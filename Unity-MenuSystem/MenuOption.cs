using System;
using System.Collections;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MenuOption : CursorSelectable
{
    public bool DisableOnPC;
    public bool DisableOnConsole;
    public enum MenuOptionType
    {
        Slider,
        Toggle,
        Menu,
        Scene,
        ControlSetup,
        CloseMenu,
        SendMessage,
        Return,
        DisableCursor,
        GameSettingList
    }

    [HideInInspector] public Menu Parent;
    [HideInInspector] public bool IsHighlighted;
    public MenuOptionType MyMenuOptionType;
    public GameSetting Setting;

    public Vector3 CursorPosition
    {
        get
        {
            return RectTransform.anchoredPosition.Offset(-RectTransform.sizeDelta.x / 2, 0);
            // return RectTransform.anchoredPosition.Offset(-RectTransform.sizeDelta.x / 2 - 64 - 16 + Mathf.Sin(Time.unscaledTime * 5) * 6, 0);
        }
    }
    public Vector2 CursorSize
    {
        get
        {
            return new Vector2(_displayText.preferredWidth, _displayText.preferredHeight);
        }
    }

    public float Value;
    public bool ValueIsPercentage;
    public float ValueIncrement = 1;
    public float ValueMin;
    public float ValueMax;

    public Menu ActivateMenu;
    public SimpleMenu ListMenu;
    public string SelectedMessage;
    public string SelectedMessageValue;
    public string Message;
    public string MessageValue;
    public GameObject MessageReceiver;
    public string LoadScene;

    public RectTransform Toggle;
    private TextMeshProUGUI _displayText;
    public TextMeshProUGUI DataText;
    public TextMeshProUGUI DisplayText => _displayText;


    [HideInInspector] public bool Waiting;
    public InputActionReference ActionReference;
    private InputActionRebindingExtensions.RebindingOperation RebindOperation;

    private StringBuilder _sb = new StringBuilder();

    public Image DeselectedGraphic;
    public Image ArrowLeft;
    public Image ArrowRight;

    [Tooltip("Optional condition - when set, this option is only active while it returns true (e.g. a permission or license check).")]
    public Func<bool> RequiredCondition;

    public override void Initialize()
    {
        base.Initialize();
        if (RequiredCondition != null)
            gameObject.SetActive(RequiredCondition());
    }

    public override void Awake()
    {
        base.Awake();
        RectTransform = GetComponent<RectTransform>();
        Toggle = transform.Find("Toggle").GetComponent<RectTransform>();
        Toggle.gameObject.SetActive(false);
        _displayText = GetComponentInChildren<TextMeshProUGUI>();
    }

    public override void Start()
    {
        //AUTO-CONFIGURE from GameSetting
        if (Setting != null)
        {
            ValueMin = Setting.Min;
            ValueMax = Setting.Max;
            ValueIncrement = Setting.Increment;
            ValueIsPercentage = Setting.IsPercentage;
        }

        base.Start();

        //LOAD from setting
        if (Setting != null)
            Value = Setting.Get();

        if (MyMenuOptionType != MenuOptionType.Toggle)
            Value = Mathf.Clamp(Value, ValueMin, ValueMax);
    }

    public void OnEnable()
    {
        if (Parent == null) Parent = GetComponentInParent<Menu>();
        if ((DisableOnConsole && MenuInput.IsOnConsole)
        || (DisableOnPC && !MenuInput.IsOnConsole))
        {
            gameObject.SetActive(false);
            return;
        }
        // Parent._menuItems.Add(this);
        // Parent._menuItems.Sort((x, y) => x.transform.GetSiblingIndex().CompareTo(y.transform.GetSiblingIndex()));
    }

    void OnDisable()
    {
        // Parent._menuItems.Remove(this);
    }

    public override void Update()
    {
        base.Update();
        if (Parent == null || !Parent.IsActive) return;

        if (DeselectedGraphic != null) DeselectedGraphic.enabled = !IsHighlighted;

        //Typing
        if (_isTyping)
        {
            _displayText.text = _typedText;
            if (!string.IsNullOrEmpty(MenuInput.LastTypedLetter))
            {
                _typedText += MenuInput.LastTypedLetter;
                MenuAudio.Play(MenuAudio.Move);
                MenuInput.LastTypedLetter = "";
            }
        }
        //Menu Option Types
        if (MyMenuOptionType == MenuOptionType.Toggle) Toggle.gameObject.SetActive(Value == 1);

        if (MyMenuOptionType == MenuOptionType.Slider)
        {
            _sb.Clear();
            _sb.Append(Value.ToString());
            if (ValueIsPercentage)
                _sb.Append("%");
            DataText.text = _sb.ToString();
        }

        //GAME SETTING sync + value text override
        if (Setting != null && (MyMenuOptionType == MenuOptionType.Slider || MyMenuOptionType == MenuOptionType.Toggle))
        {
            Setting.Set(Value);
            string vt = Setting.GetValueText(Value);
            if (vt != null) DataText.text = vt;
        }
        else if (MyMenuOptionType == MenuOptionType.GameSettingList && Setting != null)
        {
            int idx = Setting.GetInt();
            if (idx >= 0 && idx < Setting.OptionList.Count)
                DataText.text = Setting.OptionList[idx];
        }
        else if (MyMenuOptionType == MenuOptionType.ControlSetup)
        {
            if (!Waiting) DataText.text = MenuInput.GetButtonName(ActionReference);
        }
        else
        {
            DataText?.gameObject.SetActive(false);
        }
        if (ClickedInto)
        {
            ArrowLeft.gameObject.SetActive(Value > ValueMin);
            ArrowRight.gameObject.SetActive(Value < ValueMax);
            ArrowLeft.rectTransform.anchoredPosition = new Vector3(16 - 8 * Mathf.Sin(Time.unscaledTime * 10f), 0);
            ArrowRight.rectTransform.anchoredPosition = new Vector3(-16 + 8 * Mathf.Sin(Time.unscaledTime * 10f), 0);
        }
        else
        {
            ArrowLeft?.gameObject.SetActive(false);
            ArrowRight?.gameObject.SetActive(false);
        }
    }

    public override bool Click(CursorObject cursor)
    {
        base.Click(cursor);
        if (MyMenuOptionType == MenuOptionType.Return)
        {
            Parent.GoBack();
        }
        if (MyMenuOptionType == MenuOptionType.CloseMenu)
        {
            Parent.DeactivateMenu();
        }
        if (MyMenuOptionType == MenuOptionType.ControlSetup)
        {
            ClickedInto = true;
            if (ActionReference.action.enabled)
            {
                //TODO: make arrow keys / same buttons not work
                ActionReference.action.Disable();
                Debug.Log("Press button for " + ActionReference.action.name);
                Waiting = true;
                DataText.text = "Press a button.";
                RebindOperation?.Dispose();
                RebindOperation = ActionReference.action.PerformInteractiveRebinding()
                .WithControlsExcluding("Mouse")
                .WithCancelingThrough("<Keyboard>/escape")
                .OnMatchWaitForAnother(0.1f)
                .OnApplyBinding((op, keypath) =>
                {
                    if (IsPathAlreadyBoundToSomething(keypath, ActionReference))
                    {
                        RebindOperation?.Cancel();
                        EndRebinding();
                    }
                    else
                    {
                        ActionReference.action.ChangeBinding(MenuInput.Instance.BindingIndex).WithPath(keypath);
                        Debug.Log(ActionReference.action.name + " apply binding: " + keypath);
                    }
                })
                .OnComplete((op) =>
                {
                    EndRebinding();
                    MenuInput.OnBindingChanged?.Invoke(ActionReference.action);
                });
                RebindOperation.Start();
            }
        }
        if (MyMenuOptionType == MenuOptionType.Toggle)
        {
            if (Value == 1) Value = 0;
            else if (Value == 0) Value = 1;
        }
        if (MyMenuOptionType == MenuOptionType.Menu)
        {
            MenuInput.WaitForLateUpdate = true;
            if (Parent)
            {
                Parent.WentIntoSubMenu = true;
                Parent.SavedCursorSelectable = Parent.MyCursor.CurrentSelectable;
                Parent.DeactivateMenu();
            }
            ActivateMenu.PreviousMenu = Parent;
            ActivateMenu.ActivateMenu();
        }
        if (MyMenuOptionType == MenuOptionType.Scene)
        {
            if (Parent) Parent.DeactivateMenu();
            SceneManager.LoadScene(LoadScene);
        }
        //Send message on click
        if (!string.IsNullOrEmpty(Message))
        {
            if (string.IsNullOrEmpty(MessageValue))
                MessageReceiver.SendMessage(Message, SendMessageOptions.RequireReceiver);
            else
                MessageReceiver.SendMessage(Message, MessageValue, SendMessageOptions.RequireReceiver);
        }
        if (MyMenuOptionType == MenuOptionType.DisableCursor)
        {
            if (Parent != null && Parent.MyCursor != null)
                Parent.MyCursor.MovementDisabled = true;
        }
        if (MyMenuOptionType == MenuOptionType.GameSettingList)
        {
            if (Setting != null && ListMenu != null && Parent != null && Parent.MyCursor != null)
            {
                ListMenu.ClearOptions();
                for (int i = 0; i < Setting.OptionList.Count; i++)
                {
                    int capturedIndex = i;
                    ListMenu.AddOption(
                        Setting.OptionList[i],
                        () =>
                        {
                            Setting.Set(capturedIndex);
                            ListMenu.Hide();
                        }
                    );
                }
                ListMenu.CancelEnabled = true;
                ListMenu.Show(Parent.MyCursor);
            }
            return false;
        }
        if (MyMenuOptionType == MenuOptionType.Slider)
        {
            ClickedInto = !ClickedInto;
            return true;
        }
        return false;
    }

    public override void CursorMoved(CursorObject cursor, Vector2 direction)
    {
        if (Selected)
        {
            //Send message on selected
            if (!string.IsNullOrEmpty(SelectedMessage))
            {
                if (string.IsNullOrEmpty(SelectedMessageValue))
                    MessageReceiver.SendMessage(SelectedMessage, SendMessageOptions.RequireReceiver);
                else
                    MessageReceiver.SendMessage(SelectedMessage, SelectedMessageValue, SendMessageOptions.RequireReceiver);
            }
        }
        if (ClickedInto)
        {
            if (direction.x > 0 || direction.y > 0) Increment();
            if (direction.x < 0 || direction.y < 0) Decrement();
        }
    }

    public void Increment()
    {
        Value = Mathf.Clamp(Value + ValueIncrement, ValueMin, ValueMax);
        if (MyMenuOptionType == MenuOptionType.Toggle) Value = 1;
    }

    public void Decrement()
    {
        Value = Mathf.Clamp(Value - ValueIncrement, ValueMin, ValueMax);
        if (MyMenuOptionType == MenuOptionType.Toggle) Value = 0;
    }

    private bool IsPathAlreadyBoundToSomethingHelper(string path, InputActionReference actionRef, InputActionReference actionToRebind)
    {
        if (actionToRebind.action.name == actionRef.action.name) return false; //dont check for same action
                                                                               //Check all bindings for duplicate path
        for (int i = 0; i < actionRef.action.bindings.Count; i++)
        {
            var whyDoTheyHaveTwoDifferentWaysOfDisplayingPaths = path.Replace("<", "/").Replace(">", "");
            Debug.Log("CHECK BINDINGS FOR" + actionRef.action.name + " '" + path + "' == '" + actionRef.action.bindings[i].path + "'");
            if (path == actionRef.action.bindings[i].path
            || whyDoTheyHaveTwoDifferentWaysOfDisplayingPaths == actionRef.action.bindings[i].path)
            {
                SwapBindings(actionRef, actionToRebind);
                return true;
            }
        }
        return false;
    }

    private void SwapBindings(InputActionReference actionRef1, InputActionReference actionRef2)
    {
        int index = MenuInput.Instance != null ? MenuInput.Instance.BindingIndex : 0;
        var binding1 = actionRef1.action.bindings[index].path;
        var binding2 = actionRef2.action.bindings[index].path;
        actionRef1.action.ChangeBinding(index).WithPath(binding2);
        actionRef2.action.ChangeBinding(index).WithPath(binding1);

        MenuInput.OnBindingChanged?.Invoke(actionRef1.action);
        MenuInput.OnBindingChanged?.Invoke(actionRef2.action);

        Debug.Log("SWAPPED bindings " + actionRef1.action.name + " & " + actionRef2.action.name);
    }

    private bool IsPathAlreadyBoundToSomething(string path, InputActionReference actionToRebind)
    {
        //Check all registered bindable actions for duplicate path
        if (MenuInput.Instance == null || MenuInput.Instance.BindableActions == null) return false;
        foreach (var actionRef in MenuInput.Instance.BindableActions)
            if (actionRef != null && IsPathAlreadyBoundToSomethingHelper(path, actionRef, actionToRebind))
                return true;
        return false;
    }

    private void EndRebinding()
    {
        MenuInput.WaitForLateUpdate = true;
        RebindOperation?.Dispose();
        RebindOperation = null;
        Waiting = false;
        ActionReference.action.Enable();
    }

    private bool _isTyping;
    private string _typedText;
    public void BeginTyping()
    {
        ClickedInto = true;
        _isTyping = true;
    }
}
