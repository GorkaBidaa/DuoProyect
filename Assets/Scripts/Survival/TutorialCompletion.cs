using UnityEngine;
using UnityEngine.Events;

public class TutorialCompletion : MonoBehaviour
{
    [SerializeField] private UnityEvent onCompleted = new UnityEvent();
    public bool IsCompleted { get; private set; }

    public bool Complete()
    {
        if (IsCompleted) return false;
        IsCompleted = true;
        Debug.Log("Tutorial complete: the special key unlocked the gate.", this);
        onCompleted.Invoke();
        return true;
    }
}
