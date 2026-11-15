using System;
using UnityEngine;
using UnityEngine.InputSystem;

// The menu and gameplay each own an action clone; only saved overrides are shared.
public static class InputBindingSettings
{
    private const string SaveKey = "DuoProyect.InputBindings.v1";
    public static event Action Changed;

    public static void Load(InputActionAsset actions)
    {
        if (!actions) return;
        actions.RemoveAllBindingOverrides();
        string saved = PlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(saved)) return;
        try { actions.LoadBindingOverridesFromJson(saved); }
        catch (Exception error)
        {
            actions.RemoveAllBindingOverrides();
            Debug.LogWarning("Could not load saved bindings. Using defaults for this session: " + error.Message);
        }
    }

    public static void Save(InputActionAsset actions)
    {
        if (!actions) return;
        PlayerPrefs.SetString(SaveKey, actions.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
        Changed?.Invoke();
    }

    public static void RestoreDefaults()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        Changed?.Invoke();
    }
}
