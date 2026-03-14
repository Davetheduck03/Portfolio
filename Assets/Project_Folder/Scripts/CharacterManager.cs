using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterManager : MonoBehaviour
{
	public static CharacterManager Instance { get; private set; }

	[SerializeField] private BaseCharacter[] characters;

	private int _activeIndex;

	public BaseCharacter ActiveCharacter => characters[_activeIndex];

	public event System.Action<BaseCharacter> OnCharacterSwitched;

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
}