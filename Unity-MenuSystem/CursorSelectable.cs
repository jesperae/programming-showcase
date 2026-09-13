using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class CursorSelectable : MonoBehaviour
{
    [HideInInspector] public bool ClickedInto; //Has been pressed and active for controlling
    [HideInInspector] public string Name;
    public string Title;
    [SerializeField][TextArea] protected string _description;
    [Tooltip("Optional cost/requirement text shown in the cursor's cost field. Empty = hidden.")]
    public string CostDisplay;
    [HideInInspector] public RectTransform RectTransform;
    protected bool _selected;
    public bool Selected => _selected;
    [HideInInspector] public CursorSelectableDropdown MyDropdown;

    public SimpleMenu SubMenu;

    public bool DisableSelectionFade = false;
    //Played (Hide/Show) by a popup menu while it is open over this slot. If not assigned, the
    //slot's gameObject is simply disabled/enabled instead.
    public GameAnimation PopupOpenAnimation;

    //Effects
    protected float _imageAlpha = 0.4f;
    protected float _scaleMultiplier = 1;
    [HideInInspector] public Vector3 SavedAnchoredPosition;
    [HideInInspector] public float Shake;
    protected Image[] _images;

    public AudioClip PlopSound;
    public ParticleSystem PlopParticles;

    //Child transform that gets detached at runtime and rendered on the menu effect layer,
    //above menus but below the cursor. Assign particles/sounds here.
    public RectTransform VisualEffectRoot;

    //Position offset applied on top of SavedAnchoredPosition. Used by SimpleGrid for shake/sine.
    [HideInInspector] public Vector2 PositionOffset { get; protected set; }
    private Vector2 _shakeOffset;
    private Vector2 _sineOffset;

    public Vector2 SineAmplitude;
    public float SineFrequency;
    public float SinePhase;
    public bool UseUnscaledTime = true;

    private bool _dropDownIsOpenContainingThis = true;
    private bool _enabled = true;
    [HideInInspector]
    public bool IsEnabled => _enabled;
    public virtual bool CalculateIsEnabled()
    {
        _dropDownIsOpenContainingThis = CursorSelectableDropdown.IsAPartOfTheOpenedDropDownOrNoneIsOpen(this);
        // if (MyDropdown != null && MyDropdown.CursorSelectables.Count > 0 && MyDropdown.CursorSelectables.All(s => !s.IsEnabled))
        // {
        // 	return false;
        // }
        return _dropDownIsOpenContainingThis;
    }
    [HideInInspector] public bool IsADropDown;

    //NAVIGATION HIT AREA - used by CursorObject directional navigation
    //Set width/height to define the catchment zone; 0 = use rect size
    public Vector2 RaycastArea = Vector2.zero;

    public virtual void Awake()
    {
        Name = name;
        _scaleMultiplier = transform.localScale.x;
        RectTransform = GetComponent<RectTransform>();
        SavedAnchoredPosition = RectTransform.anchoredPosition;
        DetachVisualEffectRoot();
    }

    public virtual void Start()
    {
        _images = GetComponentsInChildren<Image>();
        Initialize();
    }

    public virtual void Update()
    {
        if (DisableSelectionFade) return;

        //VISUAL STATE
        if (_selected)
        {
            _imageAlpha = _imageAlpha.SmoothTowards(0.9f, 0.3f);
        }
        else
        {
            _imageAlpha = _imageAlpha.SmoothTowards(0.4f, 0.3f);
        }
        for (int i = 0; i < _images.Length; i++)
        {
            var clr = _images[i].color;
            clr.a = _imageAlpha;
            _images[i].color = clr;
        }
    }

    void LateUpdate()
    {
        UpdateShakeOffset();
        UpdateSineOffset();
        PositionOffset = _shakeOffset + _sineOffset;
        ApplyPositionOffset();
        UpdateEffectRoot();
    }

    private void UpdateShakeOffset()
    {
        if (Shake > 0)
        {
            Shake = Mathf.MoveTowards(Shake, 0f, Time.unscaledDeltaTime * 120f);
            float t = Time.unscaledTime;
            float x = Mathf.Sin(t * 50f) + Mathf.Sin(t * 73f) * 0.5f;
            float y = Mathf.Cos(t * 55f) + Mathf.Cos(t * 81f) * 0.5f;
            _shakeOffset = new Vector2(x, y) * Shake;
        }
        else
        {
            _shakeOffset = Vector2.zero;
        }
    }

    private void UpdateSineOffset()
    {
        if (SineAmplitude == Vector2.zero || SineFrequency <= 0f)
        {
            _sineOffset = Vector2.zero;
            return;
        }
        float t = (UseUnscaledTime ? Time.unscaledTime : Time.time) * SineFrequency + SinePhase;
        _sineOffset = new Vector2(
            SineAmplitude.x * Mathf.Sin(t),
            SineAmplitude.y * Mathf.Sin(t + Mathf.PI * 0.5f));
    }

    protected virtual void ApplyPositionOffset()
    {
        if (RectTransform == null) return;
        RectTransform.anchoredPosition = SavedAnchoredPosition + (Vector3)PositionOffset;
    }

    private void DetachVisualEffectRoot()
    {
        if (VisualEffectRoot == null) return;
        var layer = MenuEffectLayer.GetOrCreate(transform.root);
        VisualEffectRoot.SetParent(layer, true);
        VisualEffectRoot.SetAsLastSibling();
        var follower = VisualEffectRoot.GetComponent<FollowSelectable>();
        if (follower == null) follower = VisualEffectRoot.gameObject.AddComponent<FollowSelectable>();
        follower.Target = this;
    }

    private void UpdateEffectRoot()
    {
        if (VisualEffectRoot == null) return;
        VisualEffectRoot.gameObject.SetActive(gameObject.activeSelf);
    }

    #region SELECTION
    public void SetSelected()
    {
        if (_selected) return;
        _selected = true;
    }

    public void Deselect()
    {
        if (!_selected) return;
        _selected = false;
    }
    #endregion

    public virtual bool Click(CursorObject cursor)
    {
        Shake = 12;
        if (MyDropdown != null) MyDropdown.ChildClicked(this);
        MenuAudio.Play(MenuAudio.Confirm);

        if (SubMenu != null)
            OpenSubMenu(cursor);

        return true;
    }

    public virtual void OpenSubMenu(CursorObject cursor = null)
    {
        if (SubMenu == null) return;
        SubMenu.Show(cursor);
    }

    public virtual void CursorMoved(CursorObject cursor, Vector2 direction)
    {
    }

    public virtual string GetDescription()
    {
        return _description;
    }
    public virtual string GetTitle()
    {
        return Title;
    }

    /// Set to true in subclasses that assign Title/Description at runtime so the inspector hides those fields.
    public virtual bool ControlsTitleDescription => false;
    public virtual string GetStatsText()
    {
        return null;
    }

    public void SetDescription(string description)
    {
        _description = description;
    }

    /// Generic "look here, something new just arrived" focus feedback. Called by MenuAutomation
    /// when a node or other element is revealed. Subclasses can override for custom effects.
    public virtual void Plop()
    {
        Shake = 50;
        MenuAudio.Play(PlopSound);
        if (PlopParticles != null) PlopParticles.Play();
        PlayObtainedSounds();
    }

    /// Called from Plop() for reveals. Subclasses can play additional sounds here.
    protected virtual void PlayObtainedSounds() { }

    public virtual void Initialize()
    {
        _enabled = CalculateIsEnabled();

        //DROPDOWN CHILDREN - only activate if dropdown is open
        if (MyDropdown != null && !MyDropdown.IsOpen)
            return;

        gameObject.SetActive(_enabled);
    }

    public Vector2 GetRaycastAreaSize()
    {
        if (RectTransform == null) return RaycastArea;
        var rectSize = new Vector2(
            RectTransform.rect.width * RectTransform.lossyScale.x,
            RectTransform.rect.height * RectTransform.lossyScale.y);
        return new Vector2(
            RaycastArea.x > 0 ? RaycastArea.x : rectSize.x,
            RaycastArea.y > 0 ? RaycastArea.y : rectSize.y);
    }

    public Vector2 GetWorldCenter()
    {
        if (RectTransform == null) return transform.position;
        var pivot = RectTransform.pivot;
        var worldSize = new Vector2(RectTransform.rect.width * RectTransform.lossyScale.x, RectTransform.rect.height * RectTransform.lossyScale.y);
        var pivotOffset = new Vector2((0.5f - pivot.x) * worldSize.x, (0.5f - pivot.y) * worldSize.y);
        return (Vector2)RectTransform.position + pivotOffset;
    }

    public virtual void OnDrawGizmos()
    {
        Gizmos.color = Color.white;
        var lineLength = 2.25f;

        //RAYCAST AREA - draw when selected in editor
#if UNITY_EDITOR
        if (UnityEditor.Selection.activeGameObject == gameObject)
        {
            if (Application.isPlaying ? RectTransform != null : GetComponent<RectTransform>() != null)
            {
                var rt = Application.isPlaying ? RectTransform : GetComponent<RectTransform>();
                var pivot = rt.pivot;
                var worldSize = new Vector2(rt.rect.width * rt.lossyScale.x, rt.rect.height * rt.lossyScale.y);
                var pivotOffset = new Vector2((0.5f - pivot.x) * worldSize.x, (0.5f - pivot.y) * worldSize.y);
                Vector2 center = (Vector2)rt.position + pivotOffset;
                var areaSize = new Vector2(
                    RaycastArea.x > 0 ? RaycastArea.x : worldSize.x,
                    RaycastArea.y > 0 ? RaycastArea.y : worldSize.y);
                Gizmos.color = new Color(0f, 1f, 0.4f, 0.5f);
                Gizmos.DrawWireCube(center, areaSize);
                Gizmos.color = new Color(0f, 1f, 0.4f, 0.1f);
                Gizmos.DrawCube(center, areaSize);
            }
        }
#endif
    }
}
