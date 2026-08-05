using UnityEngine;
using UnityEngine.UI;

public class CharacterSelection : MonoBehaviour
{
    [Header("Players")]
    public GameObject[] players; // 0 = default, 1 = second, 2 = third

    [Header("UI Buttons")]
    public Button[] characterButtons; // buttons for each character
    public Text[] buttonTexts; // optional text on buttons ("Select" / "Unlock 100 coins")

    [Header("Settings")]
    public int unlockCost = 100; // ?? Cost to unlock each new character
    public Text coinText;

    void Start()
    {
        InitializeUnlocks();
        SetActivePlayer(PlayerPrefs.GetInt("SelectedPlayer", 0));
        UpdateButtonStates();
    }

    void Update()
    {
        int coins = PlayerPrefs.GetInt("Coins", 0);

        if (coinText != null)
            coinText.text = coins.ToString();

        // 🔄 Continuously check if any locked characters can be unlocked
        AutoUnlockCharacters(coins);

        UpdateButtonStates();
    }

    private void AutoUnlockCharacters(int coins)
    {
        // Loop through all characters except the first (default)
        for (int i = 1; i < players.Length; i++)
        {
            if (!IsUnlocked(i) && coins >= unlockCost)
            {
                PlayerPrefs.SetInt("Character" + i + "_Unlocked", 1);
                PlayerPrefs.Save();
                Debug.Log("✅ Auto-unlocked Character " + i);
            }
        }
    }


    // ?? Called when you press a character button
    public void OnCharacterButtonPressed(int index)
    {
        if (IsUnlocked(index))
        {
            SetActivePlayer(index);
        }
        else
        {
            TryUnlockCharacter(index);
        }
    }

    private void InitializeUnlocks()
    {
        // Player 0 always unlocked
        if (!PlayerPrefs.HasKey("Character0_Unlocked"))
            PlayerPrefs.SetInt("Character0_Unlocked", 1);
    }

    private bool IsUnlocked(int index)
    {
        return PlayerPrefs.GetInt("Character" + index + "_Unlocked", index == 0 ? 1 : 0) == 1;
    }

    private void TryUnlockCharacter(int index)
    {
        int coins = PlayerPrefs.GetInt("Coins", 0);

        if (coins >= unlockCost)
        {
            // ? Deduct coins
            coins -= unlockCost;
            PlayerPrefs.SetInt("Coins", coins);

            // ? Mark character unlocked
            PlayerPrefs.SetInt("Character" + index + "_Unlocked", 1);
            PlayerPrefs.Save();

            Debug.Log("? Unlocked Character " + index);
            UpdateButtonStates();
        }
        else
        {
            Debug.Log($"? Not enough coins! Need {unlockCost} coins to unlock.");
        }
    }

    private void SetActivePlayer(int index)
    {
        for (int i = 0; i < players.Length; i++)
        {
            players[i].SetActive(i == index);
        }

        PlayerPrefs.SetInt("SelectedPlayer", index);
        PlayerPrefs.Save();

       

        // 👇 Update GameManager references
        GameObject newPlayer = players[index];

        if (GameManager.instance != null)
        {
            GameManager.instance.player = newPlayer;
            GameManager.instance.SetCurrentPlayer(newPlayer.transform);
            GameManager.instance.SetPlayerAnimator(newPlayer.GetComponent<Animator>());
        }
    }



    private void UpdateButtonStates()
    {
        for (int i = 0; i < characterButtons.Length; i++)
        {
            bool unlocked = IsUnlocked(i);
            characterButtons[i].interactable = unlocked;

            if (buttonTexts != null && buttonTexts.Length > i)
            {
                buttonTexts[i].text = unlocked
                    ? "Select"
                    : $"Unlock ({unlockCost} Coins)";
            }
        }
    }
}
