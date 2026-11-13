using System;



using UnityEngine;



using UnityEngine.InputSystem;







[DefaultExecutionOrder(-200)]



public class GameInput : MonoBehaviour



{



    public static GameInput Instance { get; private set; }



    [SerializeField] private InputActionAsset actions;



    [SerializeField] private StaminaSystem player;



    [SerializeField] private InventoryMenu inventoryMenu;



    [SerializeField] private CraftingMenu craftingMenu;



    [SerializeField] private RopeUse ropeUse;



    private InputActionAsset runtimeActions;



    private InputActionMap gameplay;



    private float scrollDelta;



    private int scrollFrame = -1;



    private int blockedFrame = -1, interactionFrame = -1;



    private bool externalBlock;
    private bool settingsBlocked;
    public InputActionMap GameplayActions => gameplay;



    private const string SaveKey = "DuoProyect.InputBindings.v1";



    public bool UsingGamepad { get; private set; }



    public event Action BindingsChanged;







    private void Awake()



    {



        if (Instance && Instance != this) { Debug.LogError("Keep only one GameInput in the scene.", this); enabled = false; return; }



        if (!actions || !player || !inventoryMenu || !craftingMenu)



        { Debug.LogError("Assign GameInput's actions, player, inventory menu and crafting menu.", this); enabled = false; return; }



        runtimeActions = Instantiate(actions);



        gameplay = runtimeActions.FindActionMap("Gameplay", true);



        string saved = PlayerPrefs.GetString(SaveKey, "");



        if (!string.IsNullOrEmpty(saved))



        {



            try { runtimeActions.LoadBindingOverridesFromJson(saved); }



            catch (Exception e) { Debug.LogWarning("Could not load saved bindings: " + e.Message, this); }



        }



        Action("CycleItem").performed += CaptureScroll;



        Instance = this;



    }







    private void OnEnable()



    {



        if (gameplay != null) { Instance = this; gameplay.Enable(); }



    }







    private void OnDisable()



    {



        gameplay?.Disable();



        if (Instance == this) Instance = null;



    }







    private void OnDestroy() { if (runtimeActions) Destroy(runtimeActions); }







    private void Update()



    {



        // Remember the active device for prompts. Ignore gamepad stick drift.



        foreach (InputAction action in gameplay.actions)



        {



            if (action.WasPressedThisFrame() || (action.name == "Move" && Mathf.Abs(action.ReadValue<float>()) > 0.2f))



            {



                if (action.activeControl != null) UsingGamepad = action.activeControl.device is Gamepad;



            }



        }



        // Latch menu input before other scripts run, preventing order-dependent item use.



        if (Pressed("Inventory") || (ContextInteractionAvailable && Pressed("Interact")))



            BlockThisFrame();



        if (inventoryMenu.IsOpen || craftingMenu.IsOpen) BlockThisFrame();



    }







    public InputAction Action(string name) => gameplay?.FindAction(name, true);



    public bool Pressed(string name) => Action(name)?.WasPressedThisFrame() == true;



    public bool Held(string name) => Action(name)?.IsPressed() == true;



    public bool Released(string name) => Action(name)?.WasReleasedThisFrame() == true;



    public bool DialogueBlocked => externalBlock || settingsBlocked;



    public bool PromptsAllowed => !externalBlock && !settingsBlocked && Time.timeScale > 0f &&



        player && !player.IsDead && !inventoryMenu.IsOpen && !craftingMenu.IsOpen;



    public bool GameplayBlocked => externalBlock || settingsBlocked || Time.timeScale <= 0f ||



        (player && player.IsDead) || inventoryMenu.IsOpen || craftingMenu.IsOpen || blockedFrame == Time.frameCount;



    public float Move => GameplayBlocked ? 0f : Action("Move").ReadValue<float>();



    public float Cycle => GameplayBlocked || scrollFrame != Time.frameCount ? 0f : scrollDelta;







    private void CaptureScroll(InputAction.CallbackContext context)



    {



        float delta = context.ReadValue<float>();



        if (Mathf.Approximately(delta, 0f)) return;



        if (scrollFrame != Time.frameCount) scrollDelta = 0f;



        scrollFrame = Time.frameCount;



        scrollDelta += delta;



        UsingGamepad = context.control.device is Gamepad;



    }



    public bool ContextInteractionAvailable => craftingMenu.IsAtBase ||

        (ropeUse && ropeUse.HasAvailableTravel) ||

        (player && InteractionPromptRegistry.Select(player.transform.position) != null);







    public bool CanUseItem => !GameplayBlocked &&



        !(Pressed("Interact") && ContextInteractionAvailable);







    public bool TryInteract()



    {



        // Opening crafting is intentionally allowed despite the same-frame gameplay latch.



        if (externalBlock || settingsBlocked || Time.timeScale <= 0f || (player && player.IsDead) ||



            inventoryMenu.IsOpen || !Pressed("Interact") || interactionFrame == Time.frameCount)



            return false;



        interactionFrame = Time.frameCount;



        BlockThisFrame();



        return true;



    }







    public void BlockThisFrame() => blockedFrame = Time.frameCount;



    public void SetSettingsBlocked(bool value) { settingsBlocked = value; BlockThisFrame(); }

    public void SetDialogueBlocked(bool value) { externalBlock = value; BlockThisFrame(); }







    public string GetBindingLabel(string name)



    {



        InputAction action = Action(name);



        string group = UsingGamepad ? "Gamepad" : "KeyboardMouse";



        for (int i = 0; i < action.bindings.Count; i++)



        {



            var binding = action.bindings[i];



            if (!binding.isComposite && !binding.isPartOfComposite &&



                !string.IsNullOrEmpty(binding.groups) && binding.groups.Contains(group) &&



                !string.IsNullOrEmpty(binding.effectivePath))



                return action.GetBindingDisplayString(i);



        }



        return "Unbound";



    }







    public void SaveBindings()



    {



        PlayerPrefs.SetString(SaveKey, runtimeActions.SaveBindingOverridesAsJson());



        PlayerPrefs.Save();



        BindingsChanged?.Invoke();



    }







    public void RestoreDefaults()



    {



        runtimeActions.RemoveAllBindingOverrides();



        PlayerPrefs.DeleteKey(SaveKey);



        PlayerPrefs.Save();



        BindingsChanged?.Invoke();



    }



}











