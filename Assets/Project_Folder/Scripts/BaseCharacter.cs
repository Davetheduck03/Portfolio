using UnityEngine;

public abstract class BaseCharacter : MonoBehaviour
{
	public bool IsActive { get; private set; }

	public virtual void OnActivated() { IsActive = true; }
	public virtual void OnDeactivated() { IsActive = false; }
	public abstract void HandleInput();
	public abstract void Tick(); // physics/movement update
}