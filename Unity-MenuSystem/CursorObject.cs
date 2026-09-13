using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using Image = UnityEngine.UI.Image;
using System.Linq;

public class CursorObject : MonoBehaviour
{
    public static CursorObject Instance { get; private set; }

    public TextMeshProEffects TitleText;
    public TextMeshProEffects DescriptionText;
    public TextMeshProEffects StatsText;
    public TextMeshProEffects CostText;
    public TextMeshProEffects MenuNameText;

    [HideInInspector] public Vector2 CurrentPosition;
    private float _clickEffect;
    public CursorSelectable[] MySelectables;
    private Image[] _images;

    public Menu MyMenu;
    private Vector2 _moveControl;
    private bool _isNotMovingX = true;
    private bool _isNotMovingY = true;
    private float _autoMoveCounter;
    private Vector2 _autoMoveDirection;

    [HideInInspector] public RectTransform RectTransform;
    public CursorSelectable CurrentSelectable;

    public bool MovementDisabled;
    private bool _canMove { get { return !MovementDisabled && (MyMenu == null || MyMenu.IsOpen()); } }
    public bool CanMove => _canMove;
    public bool SuppressNextMoveSound;


    //Delegates
    public delegate void SelectableSwitchedDelegate(CursorSelectable selectable);
    public SelectableSwitchedDelegate Selected;
    public delegate void ClickedDelegate(CursorSelectable selectable);
    public ClickedDelegate Clicked;

    void Awake()
    {
        Instance = this;
        Selected += HandleSelected;
        Clicked += HandleClicked;
        RectTransform = GetComponent<RectTransform>();
        _images = GetComponentsInChildren<Image>();
        gameObject.SetActive(false);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void HandleSelected(CursorSelectable selectable)
    {
        //DESELECT PREVIOUS
        if (CurrentSelectable != null)
            CurrentSelectable.Deselect();

        //FALLBACK if requested selectable is null or inactive
        if (selectable == null || !selectable.gameObject.activeSelf)
        {
            selectable = FindFirstActiveSelectable();
            if (selectable == null)
            {
                CurrentSelectable = null;
                return;
            }
        }

        //SELECT NEW
        CurrentSelectable = selectable;
        CurrentSelectable.SetSelected();
        //SUPPRESS sound for programmatic selections (SimpleMenu open/close)
        if (!SuppressNextMoveSound && selectable is not SimpleMenu)
            MenuAudio.Play(MenuAudio.Move);
        SuppressNextMoveSound = false;

        //UPDATE UI
        if (DescriptionText) DescriptionText.Display(CurrentSelectable.GetDescription());
        if (TitleText) TitleText.Display(CurrentSelectable.GetTitle());
        if (MenuNameText && MyMenu) MenuNameText.Display(MyMenu.name);

        //STATS TEXT
        if (StatsText)
        {
            var stats = CurrentSelectable.GetStatsText();
            if (!string.IsNullOrEmpty(stats))
                StatsText.Display(stats);
            else
                StatsText.Hide();
        }

        if (CostText)
        {
            if (!string.IsNullOrEmpty(CurrentSelectable.CostDisplay))
                CostText.Display(CurrentSelectable.CostDisplay);
            else
                CostText.Hide();
        }

        UpdateCursorPosition();
    }

    void OnEnable()
    {
        if (MenuInput.Instance == null) return;
        MenuInput.Instance.Navigate.action.performed += Move;
        MenuInput.Instance.Confirm.action.performed += Confirm;
        MenuInput.Instance.Cancel.action.performed += Cancel;
        MenuInput.Instance.Pause.action.performed += Pause;
    }

    void OnDisable()
    {
        if (MenuInput.Instance == null) return;
        MenuInput.Instance.Navigate.action.performed -= Move;
        MenuInput.Instance.Confirm.action.performed -= Confirm;
        MenuInput.Instance.Cancel.action.performed -= Cancel;
        MenuInput.Instance.Pause.action.performed -= Pause;
        _moveControl = Vector2.zero;
    }

    public void ResetPosition(CursorSelectable cursorSelectable)
    {
        MySelectables = MyMenu.GetComponentsInChildren<CursorSelectable>(true);
        var first = FindFirstActiveSelectable();
        if (cursorSelectable != null && cursorSelectable.gameObject.activeSelf)
            Selected(cursorSelectable);
        else if (first != null)
            Selected(first);
        else
            Debug.LogWarning("Menu " + MyMenu.name + " has no active CursorSelectables.", MyMenu.gameObject);
    }

    public void ResetPosition()
    {
        MySelectables = MyMenu.GetComponentsInChildren<CursorSelectable>(true);
        var first = FindFirstActiveSelectable();
        if (first != null) Selected(first);
        else Debug.LogWarning("Menu " + MyMenu.name + " has no active CursorSelectables.", MyMenu.gameObject);
    }

    public void RefreshCurrentSelection()
    {
        if (CurrentSelectable != null)
            HandleSelected(CurrentSelectable);
    }

    public void RefreshSelectables()
    {
        if (MyMenu == null) return;
        MySelectables = MyMenu.GetComponentsInChildren<CursorSelectable>(true);
    }

    public void RefreshSelectables(CursorSelectable[] selectables, bool selectFirst = true)
    {
        MySelectables = selectables;
        if (selectFirst && MySelectables.Length > 0)
        {
            var first = MySelectables.FirstOrDefault(s => s != null && s.gameObject.activeSelf);
            if (first != null) Selected(first);
        }
    }
    public void RefreshSelectables(Transform container, bool selectFirst = true)
    {
        if (container == null) return;
        RefreshSelectables(container.GetComponentsInChildren<CursorSelectable>(true), selectFirst);
    }
    public void RefreshSelectables(GameObject container, bool selectFirst = true)
    {
        if (container == null) return;
        RefreshSelectables(container.transform, selectFirst);
    }

    private CursorSelectable FindFirstActiveSelectable()
    {
        if (MySelectables == null) return null;
        for (int i = 0; i < MySelectables.Length; i++)
        {
            var s = MySelectables[i];
            if (s != null && s.gameObject.activeSelf)
                return s;
        }
        return null;
    }

    void Move(InputAction.CallbackContext context)
    {
        if (!_canMove) return;
        var moveValue = context.ReadValue<Vector2>();

        _autoMoveDirection = moveValue;

        //TRACK which axes are freshly pressed this event
        bool freshX = false;
        bool freshY = false;

        //Only move each direction once
        if (moveValue.x == 0) { _isNotMovingX = true; _moveControl.x = 0; } //When neutral, can move
        else if (_isNotMovingX)
        {
            _autoMoveCounter = 15;
            _moveControl.x = moveValue.x;
            _isNotMovingX = false; //Can't move until neutral again
            freshX = true;
        }
        else { _moveControl.x = 0; }

        if (moveValue.y == 0) { _isNotMovingY = true; _moveControl.y = 0; }
        else if (_isNotMovingY)
        {
            _autoMoveCounter = 15;
            _moveControl.y = moveValue.y;
            _isNotMovingY = false;
            freshY = true;
        }
        else { _moveControl.y = 0; }

        if (CurrentSelectable)
            CurrentSelectable.CursorMoved(this, moveValue);

        //ONLY move cursor on fresh press, not repeated performed callbacks while held
        if (freshX || freshY)
            MoveCursor(_moveControl);
    }

    public void Confirm(InputAction.CallbackContext context)
    {
        if (!_canMove || !MenuInput.ButtonPressedDown(context)) return;

        if (CurrentSelectable != null && !MenuInput.WaitForLateUpdate)
        {
            var clicked = CurrentSelectable;
            if (clicked.Click(this))
                Clicked(clicked);
            if (DescriptionText && CurrentSelectable != null) DescriptionText.Display(CurrentSelectable.GetDescription());
        }
    }

    void HandleClicked(CursorSelectable selectable)
    {
        //Use CurrentSelectable not the clicked one - if a dropdown child was clicked, the dropdown 
        //closes and cursor moves to parent dropdown, which SHOULD be visible
        var s = CurrentSelectable ?? selectable;
        SetVisibility(!s.ClickedInto && s.MyDropdown == null); //Hide cursor if clicking into slider or other
        _clickEffect = -3.5f;
    }

    public void Hide() { SetVisibility(false); }
    public void Show() { SetVisibility(true); }

    /// Disables cursor movement for the given duration, fades out images, then re-enables and fades back in.
    public void DisableFor(float seconds)
    {
        //RESPECT an existing lock (e.g. MenuAutomation already owns the cursor). Don't start a
        //competing timer that re-enables movement before the outer controller is finished.
        if (MovementDisabled) return;
        MovementDisabled = true;
        SetVisibility(false);
        ActionSequencer.CreateSequence(this, "CursorDisableFor")
            .AddDelay(seconds)
            .Add(() => { MovementDisabled = false; SetVisibility(true); })
            .Run();
    }


    /// Play focus animation on all text fields (Title, Description, Cost, MenuName)
    /// Waits for text to finish typing before playing focus animation.

    public void FocusAttention(float duration = 1f)
    {
        ActionSequencer.CreateSequence()
            .AddWaitWhile(() =>
                (TitleText != null && !TitleText.IsTextAnimationComplete) ||
                (DescriptionText != null && !DescriptionText.IsTextAnimationComplete) ||
                (StatsText != null && !StatsText.IsTextAnimationComplete) ||
                (CostText != null && !CostText.IsTextAnimationComplete) ||
                (MenuNameText != null && !MenuNameText.IsTextAnimationComplete))
            .Add(() =>
            {
                TitleText?.PlayFocusAnimation(duration * 1.5f);
                DescriptionText?.PlayFocusAnimation(duration);
                StatsText?.PlayFocusAnimation(duration);
                CostText?.PlayFocusAnimation(duration * 2f);
                MenuNameText?.PlayFocusAnimation(duration * 1.5f);
            })
            .Run();
    }


    /// Play focus animation only on specific text field

    public void FocusAttentionTitle(float duration = 0.5f) => TitleText?.PlayFocusAnimation(duration);
    public void FocusAttentionDescription(float duration = 0.5f) => DescriptionText?.PlayFocusAnimation(duration);
    public void FocusAttentionCost(float duration = 0.5f) => CostText?.PlayFocusAnimation(duration);

    public void SetVisibility(bool visibility)
    {
        foreach (var img in _images)
        {
            img.enabled = visibility;
        }
    }

    public void Cancel(InputAction.CallbackContext context)
    {
        if (!_canMove || !MenuInput.ButtonPressedDown(context)) return;
        if (CurrentSelectable is SimpleMenu) return; //SimpleMenu handles its own cancel

        if (CursorSelectableDropdown.DropDownIsOpen)
        {
            CursorSelectableDropdown.OpenedDropDown.Close();
        }
        else if (CurrentSelectable.ClickedInto)
        {
            CurrentSelectable.ClickedInto = false;
            MenuAudio.Play(MenuAudio.Cancel);
        }
        else if (!MenuInput.WaitForLateUpdate)
        {
            //Cursor is the authority: if we can move, we can close the menu. The cursor lock already
            //prevents backing out during active animations (node reveals, multi-node plops, etc.).
            MyMenu?.GoBack();
        }
        Show();
    }

    // go back if press pause
    public void Pause(InputAction.CallbackContext context)
    {
        if (!_canMove || !MenuInput.ButtonPressedDown(context)) return;
        if (!MenuInput.WaitForLateUpdate)
        {
            //Cursor is the authority: if we can move, we can close the menu.
            MyMenu?.GoBack();
        }
    }

    void Update()
    {
        if (!_canMove)
        {
            _moveControl = Vector2.zero;
        }

        MenuInput.FrameCountdownUnscaled(ref _autoMoveCounter);
        if (!_isNotMovingX || !_isNotMovingY)
        {
            if (_autoMoveCounter == 0)
            {
                MoveCursor(_autoMoveDirection);
                _autoMoveCounter = 4;
            }
        }

        UpdateCursorPosition();

        if (CurrentSelectable != null)
        {
            //SimpleMenu manages its own lifecycle - don't evict it mid-close
            //Use activeSelf, not IsEnabled, so reveal-group-revealed nodes with a stale _enabled flag stay valid
            if (!(CurrentSelectable is SimpleMenu) && !CurrentSelectable.gameObject.activeSelf)
                CurrentSelectable = null;
        }

        //SAFETY: never leave the cursor without a selectable if any are active
        if (CurrentSelectable == null)
        {
            var fallback = FindFirstActiveSelectable();
            if (fallback != null) Selected(fallback);
        }

        if (CurrentSelectable == null)
        {
            if (TitleText) TitleText.Hide();
            if (DescriptionText) DescriptionText.Hide();
            if (StatsText) StatsText.Hide();
        }
    }

    private void UpdateCursorPosition()
    {
        if (CurrentSelectable != null)
        {
            //PIVOT-INDEPENDENT center: offset from pivot to actual rect center in world space
            var rt = CurrentSelectable.RectTransform;
            var pivot = rt.pivot;
            var worldSize = new Vector2(rt.rect.width * rt.lossyScale.x, rt.rect.height * rt.lossyScale.y);
            var pivotOffset = new Vector2((0.5f - pivot.x) * worldSize.x, (0.5f - pivot.y) * worldSize.y);
            CurrentPosition = (Vector2)rt.position + pivotOffset;
        }
        else
            return;
        Vector2 pos = RectTransform.position;
        var size = RectTransform.sizeDelta;
        // var angle = transform.localEulerAngles;
        // var posGo = pos.SmoothTowards(CurrentPosition.Offset(0,
        //     _clickEffect
        //     + CurrentSelectable.RectTransform.sizeDelta.y * CurrentSelectable.RectTransform.localScale.y * 0.025f
        //     + Mathf.Abs(Mathf.Pow(Mathf.Sin(Mathf.Rad2Deg * Time.unscaledTime * 0.05f), 4)) * 0.5f)
        // , 0.2f);
        // angle.z = (pos.x - posGo.x) * -20;
        // pos = posGo;
        size = size.SmoothTowards(CurrentSelectable.RectTransform.sizeDelta * CurrentSelectable.RectTransform.localScale, 0.3f);
        pos.x = pos.x.SmoothTowards(CurrentPosition.x, 0.3f);
        pos.y = pos.y.SmoothTowards(CurrentPosition.y, 0.3f);
        RectTransform.position = new Vector3(pos.x, pos.y, RectTransform.position.z);
        RectTransform.sizeDelta = size;
        // transform.localEulerAngles = angle;
        _clickEffect = _clickEffect.SmoothTowards(0, 0.2f);
    }

    private void MoveCursor(Vector2 direction)
    {
        if (CurrentSelectable != null && CurrentSelectable.ClickedInto) return;
        //SIMPLE MENU - consumes movement internally via CursorMoved, don't navigate away
        if (CurrentSelectable is SimpleMenu) return;

        //RAYCAST AREA SCAN - find nearest selectable whose hit-area a ray from current center intersects
        var best = FindSelectableByRaycastArea(direction);
        //CONE FALLBACK - if no area hit, expand in 5° bands like before
        if (best == null) best = FindSelectableInCone(direction);
        if (best != null) Selected(best);
    }

    //Ray from current selectable center in direction hits candidate RaycastArea rectangles.
    //Among candidates in the correct half-plane, returns the closest hit.
    private CursorSelectable FindSelectableByRaycastArea(Vector2 direction)
    {
        Vector2 origin = CurrentPosition;
        Vector2 dir = direction.normalized;
        Vector2 perp = new Vector2(-dir.y, dir.x);

        //SWEEP: straight ray first, then side-to-side 1 unit at a time
        for (float lateral = 0f; lateral <= 50f;)
        {
            float[] offsets = lateral == 0f ? new float[] { 0f } : new float[] { lateral, -lateral };

            CursorSelectable best = null;
            float bestDist = float.MaxValue;

            foreach (var offset in offsets)
            {
                Vector2 rayOrigin = origin + perp * offset;
                var candidate = FindSelectableByRaycast(rayOrigin, dir, origin);
                if (candidate != null)
                {
                    float dist = (candidate.GetWorldCenter() - origin).sqrMagnitude;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        best = candidate;
                    }
                }
            }

            if (best != null) return best;

            lateral = lateral == 0f ? 1f : lateral + 1f;
        }

        return null;
    }

    private CursorSelectable FindSelectableByRaycast(Vector2 rayOrigin, Vector2 dir, Vector2 currentOrigin)
    {
        CursorSelectable best = null;
        float bestDist = float.MaxValue;

        for (int i = 0; i < MySelectables.Length; i++)
        {
            var s = MySelectables[i];
            if (s == null || s == CurrentSelectable) continue;
            if (!s.gameObject.activeSelf) continue;
            if (s.RectTransform == null) continue;

            Vector2 center = s.GetWorldCenter();
            Vector2 toCenter = center - currentOrigin;

            //MUST be in the forward half-plane relative to current cursor position
            if (Vector2.Dot(toCenter, dir) <= 0f) continue;

            Vector2 areaSize = s.GetRaycastAreaSize();
            float halfW = areaSize.x * 0.5f;
            float halfH = areaSize.y * 0.5f;

            //AABB slab test: does infinite ray hit this rect?
            float tMin = float.NegativeInfinity;
            float tMax = float.PositiveInfinity;

            //X slab
            if (Mathf.Abs(dir.x) > 0.0001f)
            {
                float t1 = (center.x - halfW - rayOrigin.x) / dir.x;
                float t2 = (center.x + halfW - rayOrigin.x) / dir.x;
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);
            }
            else if (rayOrigin.x < center.x - halfW || rayOrigin.x > center.x + halfW) continue;

            //Y slab
            if (Mathf.Abs(dir.y) > 0.0001f)
            {
                float t1 = (center.y - halfH - rayOrigin.y) / dir.y;
                float t2 = (center.y + halfH - rayOrigin.y) / dir.y;
                if (t1 > t2) { float tmp = t1; t1 = t2; t2 = tmp; }
                tMin = Mathf.Max(tMin, t1);
                tMax = Mathf.Min(tMax, t2);
            }
            else if (rayOrigin.y < center.y - halfH || rayOrigin.y > center.y + halfH) continue;

            if (tMax < 0f || tMin > tMax) continue; //no hit

            float hitDist = Mathf.Max(tMin, 0f);
            if (hitDist < bestDist)
            {
                bestDist = hitDist;
                best = s;
            }
        }

        return best;
    }

    private CursorSelectable FindSelectableInCone(Vector2 direction)
    {
        const float ANGLE_STEP = 5f;
        const float MAX_ANGLE = 90f;

        for (float angle = 0; angle < MAX_ANGLE; angle += ANGLE_STEP)
        {
            CursorSelectable closest = null;
            float closestDist = float.MaxValue;

            for (int i = 0; i < MySelectables.Length; i++)
            {
                if (MySelectables[i] == null) continue;
                if (MySelectables[i] == CurrentSelectable) continue;
                if (!MySelectables[i].gameObject.activeSelf) continue;
                if (MySelectables[i].RectTransform == null) continue;

                Vector2 toSelectable = MySelectables[i].GetWorldCenter() - CurrentPosition;
                float dist = toSelectable.magnitude;
                if (dist < 0.001f) continue;

                float angleTo = Vector2.Angle(direction, toSelectable);
                if (angleTo >= angle && angleTo < angle + ANGLE_STEP && dist < closestDist)
                {
                    closestDist = dist;
                    closest = MySelectables[i];
                }
            }

            if (closest != null) return closest;
        }
        return null;
    }
}
