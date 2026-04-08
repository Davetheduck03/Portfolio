using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterManager : MonoBehaviour
{
	public static CharacterManager Instance { get; private set; }

	[SerializeField] private BaseCharacter[] characters;

	private int _activeIndex;


	public BaseCharacter ActiveCharacter => characters[_activeIndex];

	public event System.Action<BaseCharacter> OnCharacterSwitched;

	public int CharacterCount => characters.Length;

	void Awake()
	{
		Instance = this;
	}

	void Start()
	{
		// Activate the first character, deactivate the rest
		for (int i = 0; i < characters.Length; i++)
		{
			if (i == _activeIndex)
				characters[i].OnActivated();
			else
				characters[i].OnDeactivated();
		}
	}

	void Update()
	{
		if (Keyboard.current.tabKey.wasPressedThisFrame)
			SwitchCharacter();

		ActiveCharacter.HandleInput();
	}

	void FixedUpdate()
	{
		ActiveCharacter.Tick();
	}

	private void SwitchCharacter()
	{
		characters[_activeIndex].OnDeactivated();
		_activeIndex = (_activeIndex + 1) % characters.Length;
		characters[_activeIndex].OnActivated();
		OnCharacterSwitched?.Invoke(characters[_activeIndex]);
	}

	/// <summary>
	/// Returns true if any character's XY position is within <paramref name="radius"/>
	/// of <paramref name="worldPos"/>. Used to prevent placing boxes on characters.
	/// </summary>
	public bool IsOccupiedByCharacter(Vector3 worldPos, float radius)
	{
		float sqrRadius = radius * radius;
		foreach (BaseCharacter c in characters)
		{
			float dx = c.transform.position.x - worldPos.x;
			float dy = c.transform.position.y - worldPos.y;
			if (dx * dx + dy * dy < sqrRadius) return true;
		}
		return false;
	}
}