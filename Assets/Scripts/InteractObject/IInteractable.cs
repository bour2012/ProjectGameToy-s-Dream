using UnityEngine;

 public interface IInteractable
{
    string GetInteractText();
    void Interact();
    Vector3 GetUiOffset() => Vector3.zero;
}
