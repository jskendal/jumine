using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine.UI;
using System.Threading.Tasks;

public class MainMenuManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject matchmakingPanel;

    [Header("Matchmaking UI")]
    public TextMeshProUGUI searchText;
    public TextMeshProUGUI countPlayersText;

    [Header("Scene Names")]
    public string gameSceneName = "scene";

    private SimpleWebSocketClient wsClient;
    private int playersInQueue = 0;
    private List<int> agreedPlayers = new List<int>();
    
    public GameObject fillAiButton;
    public Transform circlesParent; // 🔥 Glisse ici un enfant vide du bouton Fill AI
    public GameObject circlePrefab; // 🔥 Un petit cercle UI (Image ronde)

    public GameManager gameManager;
 
    [Header("Options")]
    public GameObject optionsPanel;
    public TMP_InputField nicknameInput;

    private string playerNickname = "Player1";

    void Start()
    {
        ShowMainMenu();
        wsClient = FindObjectOfType<SimpleWebSocketClient>();
        if (wsClient == null)
        {
            GameObject obj = new GameObject("WebSocketClient");
            wsClient = obj.AddComponent<SimpleWebSocketClient>();
            DontDestroyOnLoad(obj); // Pour qu'il survive au changement de scène
        }

        if (PlayerPrefs.HasKey("nickname"))
            playerNickname = PlayerPrefs.GetString("nickname");
        else
            playerNickname = "Player" + Random.Range(1000, 9999);
 
    }

    public void ShowMainMenu()  
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (matchmakingPanel != null) matchmakingPanel.SetActive(false);
        if (optionsPanel != null) optionsPanel.SetActive(false);
        ResetMatchmakingUI();
    }

    public void OnClickSolo()
    {
        //SceneManager.LoadScene(gameSceneName);
        mainMenuPanel.SetActive(false);
        matchmakingPanel.SetActive(true);
        
        // if (SimpleWebSocketClient.Instance == null)
        // {
        //     // Crée le GameObject s'il n'existe pas (premier lancement)
        //     var go = new GameObject("WebSocketClient");
        //     go.AddComponent<SimpleWebSocketClient>();
        // }
        //SimpleWebSocketClient.Instance.JoinSolo("Player1");
        // Lance la simulation d'ajout des IA
        StartCoroutine(SimulateSoloMatchmaking());
    }

    IEnumerator SimulateSoloMatchmaking()
    {
        // Simulation des 4 joueurs qui rejoignent
        for (int i = 1; i <= 4; i++)
        {
            if (countPlayersText != null)
                countPlayersText.text = $"{i}/4";
            
            // Petit délai réaliste entre chaque "joignage"
            yield return new WaitForSeconds(0.5f); 
        }

        // Connexion + envoi de la config Solo
        wsClient.ConnectAndJoin("solo", playerNickname);

        // Une fois 4/4 atteint, on charge le jeu
        yield return new WaitForSeconds(0.2f);
        SceneManager.LoadScene(gameSceneName);
    }
    
    public void OnClickMultiplayer()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (matchmakingPanel != null) matchmakingPanel.SetActive(true);

        if (searchText != null) searchText.text = "Searching Players...";
        if (countPlayersText != null) countPlayersText.text = "1/4";
        
        // TODO:
        // ici on branchera le websocket client
        // puis on enverra le message "join queue"
        if (wsClient != null)
        {
            wsClient.ConnectAndJoin("multi", playerNickname);
            // wsClient.Connect();
            //wsClient.JoinQueue("Player1");
            // Une fois connecté, envoyer join_queue
            // (tu peux le faire dans OnConnected du WebSocketClient)
        }
    }

    public void OnClickCancelMatchmaking()
    {
        if (wsClient != null)
            wsClient.CancelQueue();

        ShowMainMenu();

        ResetMatchmakingUI();
        // TODO:
        // ici on enverra plus tard un message websocket "leave queue"
    }

    public void OnClickFillAI()
    {
        // if (wsClient != null)
        //     wsClient.AgreeFillAI();
        
        AddAgreementCircle();
         fillAiButton.GetComponent<Button>().interactable = false;
    }

    public void OnServerMessage(string json)
    {
        try
        {
            var root = Newtonsoft.Json.Linq.JObject.Parse(json);
            string op = root["op"]?.ToString();

            switch (op)
            {
                case "queue_update":
                    int count = root["count"]?.Value<int>() ?? 0;
                    UpdateQueueCount(count);
                    break;

                case "show_fill_ai_btn":
                    int playerCount = root["playerCount"]?.Value<int>() ?? 0;
                    ShowFillAiButton(playerCount);
                    break;

                case "player_agreed_ai":
                    int playerId = root["playerId"]?.Value<int>() ?? -1;
                    OnPlayerAgreedAI(playerId);
                    break;

                case "match_found":
                    SceneManager.LoadScene("GameScene");
                    break;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("Erreur parse JSON: " + e.Message);
        }
    }

    void UpdateQueueCount(int count)
    {
        playersInQueue = count;
        if (countPlayersText != null)
            countPlayersText.text = $"{count}/4";
    }

    void ShowFillAiButton(int playerCount)
    {
        fillAiButton.SetActive(true);
        if (circlesParent != null)
        {
            foreach (Transform child in circlesParent) 
                Destroy(child.gameObject);
        }
    }

    void OnPlayerAgreedAI(int playerId)
    {
        agreedPlayers.Add(playerId);
        AddAgreementCircle();

        if (agreedPlayers.Count >= playersInQueue)
        {
            Debug.Log("Tous les joueurs ont accepté !");
            // Le serveur va envoyer match_found
        }
    }

    void AddAgreementCircle()
    {
        if (circlesParent == null || circlePrefab == null)
        {
            Debug.LogWarning("circlesParent ou circlePrefab non assigné !");
            return;
        }

        GameObject circle = Instantiate(circlePrefab, circlesParent);
        circle.name = $"Circle_{agreedPlayers.Count - 1}";
        // Réinitialiser le scale/localPosition si besoin
        circle.transform.localScale = Vector3.one;
        circle.transform.localPosition = Vector3.zero;
    }

    void ResetMatchmakingUI()
    {
        if (searchText != null) searchText.text = "Searching Players...";
        if (countPlayersText != null) countPlayersText.text = "0/4";
        
        if (fillAiButton != null)
            fillAiButton.SetActive(false);
        
        agreedPlayers.Clear();
        if (circlesParent != null)
        {
            foreach (Transform child in circlesParent) 
                Destroy(child.gameObject);
        }
    } 

    public void OnClickOptions()
    {
        mainMenuPanel.SetActive(false);
        matchmakingPanel.SetActive(false);
        optionsPanel.SetActive(true);
        
        // Afficher le nickname actuel
        if (nicknameInput != null)
            nicknameInput.text = playerNickname;
    }

    public void OnSaveOptions()
    {
        if (nicknameInput != null && !string.IsNullOrEmpty(nicknameInput.text))
        {
            playerNickname = nicknameInput.text.Trim();
            PlayerPrefs.SetString("nickname", playerNickname);
            PlayerPrefs.Save();
            Debug.Log($"Nickname saved: {playerNickname}");
        }
        
        ShowMainMenu();
    }

    public void OnBackFromOptions()
    {
        ShowMainMenu();
    }

    // On utilisera cette fonction plus tard quand le serveur renverra l'état de la queue
    public void UpdateMatchmakingCount(int current, int required)
    {
        if (countPlayersText != null)
            countPlayersText.text = $"{current}/{required}";
    }

    // On utilisera cette fonction plus tard quand le serveur dira "match found"
    public void StartMultiplayerGame()
    {
        SceneManager.LoadScene(gameSceneName);
    }
}