using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static GridManager;
using UnityEngine.UI;
using System.Linq;
using System.Threading.Tasks;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("Références")]
    public GridManager gridManager;
    public GameObject playerPrefab;
    public SimpleWebSocketClient networkClient;        
    public bool _gameStartReceived = false; 
    private GameState _lastServerState;
    private bool isDuelInProgress = false;

    [Header("Paramètres")]
    public float timeBetweenRows = 5f;
    public float selectionTime = 10f;
    
    [Header("Camera")]
    public Camera mainCamera;
    public float cameraHeight = 15f;
    public float cameraDistance = 12f;
    public float cameraAngle = 45f;
    
    [Header("Jump")]
    public float jumpHeight = 1.5f;
    public float jumpDuration = 0.5f;
    
    [Header("UI")]
    public UnityEngine.UI.Text timerText;
    public UnityEngine.UI.Slider timerSlider;
    
    // Variables
    private float rowTimer = 0f;
    private float selectionTimer = 0f;
    private bool isSelectionPhase = true;
    public GameObject[] players;

    private List<GameObject> highlightedBorders = new List<GameObject>();
    public static GameManager instance;

    [Header("Health UI")]
    public Slider[] playerHealthSliders; // Size = 4
    public TMPro.TextMeshProUGUI[] playerLabels; // Size = 4
    
    private bool hasDoneSpawnPlayers = false;

    public Vector2Int[] playerTargets = new Vector2Int[4];

    [Header("Contrôle Joueurs")]
    public int localPlayerID = 1; 
    public ControlMode[] playerControlModes = new ControlMode[4] { ControlMode.AI, ControlMode.Human, ControlMode.AI, ControlMode.AI };
    
    public List<PlayerAction> currentTurnActions = new List<PlayerAction>();

    private Queue<EffectEvent> effectQueue = new Queue<EffectEvent>();
    private Queue<EffectEvent> removeEffectQueue = new Queue<EffectEvent>();
    private bool isProcessingEffects = false;

    public Canvas gameCanvas;

    private bool areJumpsInProgress = false;

    // Structure pour stocker les événements
    public struct EffectEvent
    {
        public int playerId;
        public int launcherPlayerId;
        public EffectType effectType;
        public int value;
        public int rank;
        public EffectHitInfo[] hits;
        public Direction weaponDirection;
        public int newHealth;
        public List<int> participants;
    }

    void Start()
    {
        // if (SimpleWebSocketClient.Instance != null)
        // {
        //     SimpleWebSocketClient.Instance.gameManager = this;
        //     Debug.Log("GameManager connecté au WebSocketClient");
        // }
        networkClient = FindObjectOfType<SimpleWebSocketClient>();
        if (gameCanvas != null)
            gameCanvas.enabled = false;

        StartCoroutine(StartGameAfterGridReady());

        InitializeHealthUI();
 
    }
    
        // ═══════════════════════════════════════════
    // NOUVELLE MÉTHODE : Envoyer l'action au serveur
    // ═══════════════════════════════════════════
    
    public void SendActionToServer(int row, int col)
    {
        if (networkClient == null)
        {
            Debug.LogError("NetworkClient non assigné !");
            return;
        }

        // Envoyer JSON : {"op":"action", "row":X, "col":Y}
        string json = $"{{\"op\":\"action\", \"row\":{row}, \"col\":{col}}}";
        networkClient.Send(json);
        
        Debug.Log($"[Envoi] Action: ({row}, {col})");
    }

    void Update()
    {
            // FORCER la caméra à chaque frame pendant 0.5s
        float timer = 0f;
        timer += Time.deltaTime;
        
        if (timer < 0.5f && mainCamera != null)
        {
            mainCamera.transform.position = new Vector3(0f, cameraHeight, -cameraDistance);
            mainCamera.transform.rotation = Quaternion.Euler(cameraAngle, 0f, 0f);
        }
 
        if (isSelectionPhase)
        {
            UpdateSelectionTimer();
        }
    }

    void Awake() {
        instance = this;
        if (playerControlModes == null || playerControlModes.Length != 4)
            playerControlModes = new ControlMode[4];

        playerControlModes[0] = ControlMode.AI;
        playerControlModes[1] = ControlMode.Human;
        playerControlModes[2] = ControlMode.AI;
        playerControlModes[3] = ControlMode.AI;
    }

    IEnumerator ProcessEffectQueue()
    {
        isProcessingEffects = true;


        while (effectQueue.Count > 0)
        {
            EffectEvent evt = effectQueue.Dequeue();
            yield return StartCoroutine(OnEffectAppliedCoroutine(evt.playerId, evt.effectType, 
                                                                evt.value, evt.rank, evt.hits, evt.newHealth, evt.weaponDirection, evt.participants));
        }
        while (removeEffectQueue.Count > 0)
        {
            EffectEvent evt = removeEffectQueue.Dequeue();
            yield return StartCoroutine(OnEffectRemoved(evt.playerId, evt.effectType, evt.rank));
        }

        yield return new WaitForSeconds(0.5f);

        isProcessingEffects = false;

        // Maintenant, on peut relancer le tour suivant
        //StartSelectionPhase();
    }

    public IEnumerator OnEffectAppliedCoroutine(int playerId, EffectType effectType, 
                                            int value, int rank, EffectHitInfo[] hits, int newHealth, Direction weaponDirection = Direction.None, List<int> participants = null)
    {
         // 🔥 Le secret : chaque animation attend son tour
        yield return new WaitForSeconds(rank * 0.5f); 
        // Tu récupères le joueur et tu joues l'animation
        GameObject playerObj = players[playerId];
        switch(effectType)
        {
            case EffectType.HealthPotion:
                Debug.Log($"Anim Heal Joueur {playerId+1}");
            // Spawn 5 petites sphères vertes aléatoirement autour du joueur
                for (int i = 0; i < 5; i++)
                {
                    GameObject healParticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    healParticle.transform.position = playerObj.transform.position + Random.insideUnitSphere * 0.5f;
                    healParticle.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                    healParticle.GetComponent<Renderer>().material.color = Color.green;

                    // Faire monter la particule puis la détruire
                    StartCoroutine(MoveUpAndDestroy(healParticle));
                }
                yield return new WaitForSeconds(0.5f);
                UpdatePlayerHealthBar(playerId, newHealth);
                break;

            case EffectType.MegaJump:
                int count = Random.Range(3, 8); // Plus ou moins de particules
                
                for (int i = 0; i < count; i++)
                {
                    var part = GameObject.CreatePrimitive(PrimitiveType.Plane);
                    part.transform.position = playerObj.transform.position;
                    
                    // Variation
                    part.transform.localScale = Vector3.one * Random.Range(0.1f, 0.5f); // Taille aléatoire
                    part.transform.rotation = Quaternion.Euler(Random.Range(0, 360), 0, Random.Range(0, 360)); // Rotation aléatoire
                    
                    // Couleur variée (bleu clair à bleu foncé)
                    part.GetComponent<Renderer>().material.color = Color.Lerp(Color.cyan, Color.blue, Random.value);

                    StartCoroutine(MoveUpAndDestroy(part));
                }
                yield return new WaitForSeconds(0.5f);
                break;

            case EffectType.DamageBomb:
                Debug.Log($"[Anim] Bomb Joueur {playerId+1}");
                // Anim : secousse + flash rouge
                Vector3 originalPos = playerObj.transform.position;
                for (int i = 0; i < 3; i++)
                {
                    playerObj.transform.position += Random.insideUnitSphere * 0.1f;
                    yield return new WaitForEndOfFrame();
                }
                playerObj.transform.position = originalPos;
                yield return new WaitForSeconds(0.5f);
                UpdatePlayerHealthBar(playerId, newHealth);
                break;

            case EffectType.Spray:
                Debug.Log($"[Anim] Spray Joueur {playerId+1}");
             
                List<Vector3> sprayDirs = new List<Vector3>();
                
                if (weaponDirection == Direction.Left || weaponDirection == Direction.LeftAndRight || weaponDirection == Direction.All)
                    sprayDirs.Add(Vector3.left);
                if (weaponDirection == Direction.Right || weaponDirection == Direction.LeftAndRight || weaponDirection == Direction.All)
                    sprayDirs.Add(Vector3.right);

                if(weaponDirection == Direction.None)
                {
                    sprayDirs.Add(Vector3.right); //todo random direction vector
                }
                foreach (var baseDir in sprayDirs)
                {
                    // On crée un "nuage" de 10 particules par direction
                    for (int i = 0; i < 30; i++)
                    {
                        GameObject p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        p.transform.position = playerObj.transform.position + Vector3.up * 0.5f;
                        p.transform.localScale = Vector3.one * 0.1f;
                        p.GetComponent<Renderer>().material.color = new Color(1f, 0.5f, 0f, 0.8f); // Orange/Feu
                        Destroy(p.GetComponent<BoxCollider>()); // Important pour la perf

                        // On ajoute de la variation pour faire un "cône"
                        // On dévie la direction de base un peu vers l'avant/arrière (Z) et haut/bas (Y)
                        Vector3 spread = new Vector3(
                            baseDir.x, 
                            Random.Range(-0.2f, 0.2f), // Dispersion verticale
                            Random.Range(-0.8f, 0.8f)  // Dispersion largeur (Z) sur 3 cases
                        );

                        // On lance le mouvement (3 cases = environ cellSize * 3)
                        float sprayDistance = gridManager.cellSize * 3f;
                        StartCoroutine(MoveSprayParticle(p, spread.normalized, sprayDistance));
                    }
                }
                yield return new WaitForSeconds(0.8f); // Temps pour voir le nuage

                foreach (var hit in hits)
                {
                    UpdatePlayerHealthBar(hit.PlayerId, hit.NewHealth);
                }
                break;

            case EffectType.Laser:

                Debug.Log($"[Anim] Laser Joueur {playerId + 1}");
                int laserRow = value; // row du lanceur

                // 1. Créer le faisceau laser (un long cube fin)
                GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
                beam.GetComponent<Renderer>().material.color = new Color(0f, 1f, 1f, 0.8f); // Cyan brillant

                // Positionner au centre de la ligne
                float gridCenterX = (gridManager.GetCellWorldPosition(laserRow, 0).x + gridManager.GetCellWorldPosition(laserRow, gridManager.columns - 1).x) / 2f;
                Vector3 beamPos = new Vector3(gridCenterX, playerObj.transform.position.y + 0.5f, playerObj.transform.position.z);
                beam.transform.position = beamPos;

                // Redimensionner pour couvrir toute la largeur
                float totalWidth = Mathf.Abs(gridManager.GetCellWorldPosition(laserRow, gridManager.columns - 1).x - gridManager.GetCellWorldPosition(laserRow, 0).x);
                beam.transform.localScale = new Vector3(totalWidth, 0.02f, 0.05f);

                yield return new WaitForSeconds(0.8f); // Durée du flash laser
                Destroy(beam);

                // 2. Mettre à jour le joueurs sur la ligne
                foreach (var hit in hits)
                {
                    UpdatePlayerHealthBar(hit.PlayerId, hit.NewHealth);
                }
                break;

            case EffectType.Missile:
                int mRow = value;
                int launcherCol = _lastServerState.Players[playerId].Col;
                float startX = playerObj.transform.position.x;

                // --- Trouver les X d'arrêt pour le visuel ---
                float stopXRight = gridManager.GetCellWorldPosition(mRow, gridManager.columns - 1).x;
                float stopXLeft = gridManager.GetCellWorldPosition(mRow, 0).x;

                // On cherche les cibles réelles dans le GameState pour arrêter les missiles dessus
                foreach (var p in _lastServerState.Players)
                {
                    if (p.ID == playerId || !p.IsAlive || p.Row != mRow) continue;

                    float pX = gridManager.GetCellWorldPosition(p.Row, p.Col).x;
                    if (p.Col > launcherCol && pX < stopXRight) stopXRight = pX; // Plus proche à droite
                    if (p.Col < launcherCol && pX > stopXLeft) stopXLeft = pX;   // Plus proche à gauche
                }

                // 1. Lancement des deux projectiles
                GameObject mRight = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                mRight.transform.position = playerObj.transform.position + Vector3.up * 0.5f;
                mRight.transform.localScale = new Vector3(0.6f, 0.2f, 0.2f);
                mRight.transform.rotation = Quaternion.Euler(0, 0, 90);
                mRight.GetComponent<Renderer>().material.color = Color.yellow;

                GameObject mLeft = Instantiate(mRight, mRight.transform.position, mRight.transform.rotation);

                // 2. Animation de déplacement simultanée
                float mSpeed = 15f;
                while (mRight.transform.position.x < stopXRight || mLeft.transform.position.x > stopXLeft)
                {
                    if (mRight.transform.position.x < stopXRight)
                        mRight.transform.position += Vector3.right * mSpeed * Time.deltaTime;

                    if (mLeft.transform.position.x > stopXLeft)
                        mLeft.transform.position += Vector3.left * mSpeed * Time.deltaTime;

                    yield return null;
                }

                // 3. Explosion et Update
                Destroy(mRight); Destroy(mLeft);

                foreach (var hit in hits)
                {
                    UpdatePlayerHealthBar(hit.PlayerId, hit.NewHealth);
                }
                break;

            case EffectType.Freeze:
                Debug.Log($"[Anim] Freeze Joueur {playerId+1}");
               Renderer playerRend = playerObj.GetComponent<Renderer>();
                // On garde la couleur originale en mémoire (optionnel, mais utile si tu veux la restaurer pile poil)
                // Color originalColor = playerRend.material.color; 

                // 1. Créer le cube de glace
                GameObject iceCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                iceCube.name = "IceCube_FX"; // Nom unique
                iceCube.transform.parent = playerObj.transform;
                
                // ASTUCE CRUCIALE : On le soulève un peu (0.1f) pour éviter qu'il ne rentre dans le sol/joueur
                iceCube.transform.localPosition = Vector3.up * 0.1f; 
                
                // On l'agrandit vraiment (1.4f) pour qu'il dépasse du joueur
                iceCube.transform.localScale = Vector3.one * 1.4f; 

                // 2. Shader "Legacy/Transparent/Diffuse" (beaucoup plus fiable que Standard pour la transparence)
                Material iceMat = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
                iceMat.color = new Color(0.5f, 0.8f, 1f, 0.5f); // Bleu glace visible
                iceCube.GetComponent<Renderer>().material = iceMat;

                // 3. Changer la couleur du joueur (effet "congelé")
                playerRend.material.color = new Color(0.6f, 0.8f, 1f); 
                break;

            case EffectType.Poison:
                for (int i = 0; i < 5; i++)
                {
                    GameObject poisonParticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    poisonParticle.transform.position = playerObj.transform.position + Random.insideUnitSphere * 0.5f;
                    poisonParticle.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                    poisonParticle.GetComponent<Renderer>().material.color = new Color(0.8f, 0.2f, 0.8f); // Violet foncé

                    // Plus lent que le soin pour donner une impression de lenteur toxique
                    StartCoroutine(MoveUpAndDestroy(poisonParticle, true));
                }
                yield return new WaitForSeconds(0.5f);
                UpdatePlayerHealthBar(playerId, newHealth);
                break;

            case EffectType.Armor:
                Debug.Log($"[Anim] Armor Joueur {playerId+1}");
                // Anim : bouclier lumineux
                // 1. Créer le bouclier (une sphère autour du joueur)
                GameObject shieldSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shieldSphere.name = "Armor_FX";
                shieldSphere.transform.parent = playerObj.transform;
                shieldSphere.transform.localPosition = Vector3.zero;
                shieldSphere.transform.localScale = Vector3.one * 1.5f;

                // 2. Configurer le matériau du bouclier
                Material shieldMat = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
                shieldMat.color = new Color(0.1f, 0.4f, 1f, 0.6f); // Bleu plus foncé, moins transparent
                shieldSphere.GetComponent<Renderer>().material = shieldMat;

                // 3. Animation de pulsation (pour montrer que le bouclier est actif)
                float pulseDuration = 0.5f;
                float timer = 0f;
                Vector3 originalScale = shieldSphere.transform.localScale;

                while (timer < pulseDuration)
                {
                    float scaleMultiplier = 1f + Mathf.PingPong(timer * 2, 0.1f);
                    shieldSphere.transform.localScale = originalScale * scaleMultiplier;
                    timer += Time.deltaTime;
                    yield return null;
                }

                // Le bouclier reste à sa taille normale après la pulsation
                shieldSphere.transform.localScale = originalScale;
                break;

            case EffectType.Random:
                Debug.Log($"[Anim] Random Joueur {playerId+1}");
                
                // Le serveur nous a envoyé le type final dans 'evt.value'
                EffectType finalType = (EffectType)value;
                
                // 1. Animation "Machine à sous" sur le joueur
                // (ex: un point d'interrogation qui tourne au-dessus de sa tête)
                GameObject questionMark = GameObject.CreatePrimitive(PrimitiveType.Cube); // Remplacer par ton vrai FX
                questionMark.transform.position = playerObj.transform.position + Vector3.up * 1.5f;
                questionMark.GetComponent<Renderer>().material.color = Color.magenta;
                
                yield return new WaitForSeconds(1.0f); // Le temps de la roulette
                Destroy(questionMark);

                // 2. Changer visuellement la case en dessous du joueur 
                Vector2Int pPos = GetPlayerCurrentCell(playerId);
                
                // ✅ Utiliser la nouvelle fonction du GridManager
                if (gridManager != null)
                {
                    gridManager.ForceCellVisual(pPos.x, pPos.y, finalType);
                }
                
                // Petite pause "Révélation" avant que le vrai effet ne frappe
                yield return new WaitForSeconds(0.5f);
                break;
                
            case EffectType.CollisionDuel:
                 Debug.Log($"⚔️ Duel Visuel lancé ! Participants: {string.Join(", ", participants)}");
                if (isDuelInProgress) yield break; // Évite les doublons
                isDuelInProgress = true;
                // 1. AFFICHER LA PIÈCE QUI TOURNE
                GameObject coinFX = GameObject.CreatePrimitive(PrimitiveType.Sphere); // Ou un modèle 3D de pièce
                coinFX.name = "DuelCoin_FX";

                // Positionne la pièce au-dessus du centre de la case
                Vector3 duelCellPos = gridManager.GetCellWorldPosition(
                    _lastServerState.Players.First(p => p.ID == playerId).Row, // Position logique du joueur
                    _lastServerState.Players.First(p => p.ID == playerId).Col
                );
                // On la place au-dessus des joueurs et on la met debout (rotation sur X)
                coinFX.transform.position = duelCellPos + Vector3.up * 1.8f;
                coinFX.transform.localScale = new Vector3(0.4f, 0.02f, 0.4f); // Plat comme une pièce
                coinFX.transform.rotation = Quaternion.Euler(90f, 0f, 0f); 
                coinFX.GetComponent<Renderer>().material.color = new Color(1f, 0.84f, 0f);

                // Simple animation de rotation
                StartCoroutine(RotateCoin(coinFX.transform));

                var duel = _lastServerState.CurrentDuels.First(d => d.PlayerIDs.Contains(playerId));
                bool isHumanInvolved = participants.Contains(localPlayerID) && playerControlModes[localPlayerID] == ControlMode.Human;
                Dictionary<int, int> duelChoices = new Dictionary<int, int>();

                // 2. DÉCLENCHER LA POPUP UI (Si le joueur est humain et participant)
                if (isHumanInvolved)
                {
                    int humanChoice = 0; // 0 = Or, 1 = Argent
                    bool choiceDone = false;
                    Debug.Log($"Popup Flip Coin pour Joueur {localPlayerID + 1}");
                    DuelUIManager.Instance.StartDuel(async (choice) =>
                            {
                                humanChoice = choice;
                                duelChoices.Add(localPlayerID, humanChoice);
                                choiceDone = true;

                                // Assigner le choix inverse à l'autre joueur (l'IA)
                                int otherPlayerId = participants.First(p => p != localPlayerID);
                                duelChoices.Add(otherPlayerId, humanChoice == 0 ? 1 : 0);
                    
                                 // Envoyer le choix au serveur
                                string json = Newtonsoft.Json.JsonConvert.SerializeObject(new {
                                    op = "duel_choice",
                                    duelId = duel.DuelId, // Tu peux utiliser l'index du duel ou un ID serveur
                                    playerId = localPlayerID,
                                    duelChoices = duelChoices
                                });
                                await networkClient.Send(json);
                            });

                    while (!choiceDone) yield return null;


                }
                else
                {
                    duelChoices.Add(participants[0], 0);
                    duelChoices.Add(participants[1], 1);
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        op = "duel_choice",
                        duelId = duel.DuelId, // Tu peux utiliser l'index du duel ou un ID serveur
                        playerId = playerId,
                        duelChoices = duelChoices
                    });
                    yield return new WaitForSeconds(1.5f);
                    networkClient.Send(json);
                }

                //si outJsonAIDuel exist, alors faudrait call une async fct 
                //OnServerMessage(string json)

                // if (!isHumanInvolved)
                // {
                //     duelChoices.Add(participants[0], 0);
                //     duelChoices.Add(participants[1], 1);

                //     // Petite pause pour simuler la tension du duel
                //     yield return new WaitForSeconds(2.0f); 
                // }

                //yield return new WaitForSeconds(5.0f); // Simule le temps du choix du joueur + flip

                // 4. NETTOYER L'ANIMATION DE LA PIÈCE
                Destroy(coinFX);
 
                //yield return StartCoroutine(ResolveDuel(playerId, duelChoices, participants));
                break;
        }
    }

    IEnumerator MoveSprayParticle(GameObject p, Vector3 dir, float distance)
    {
        float timer = 0f;
        float duration = 0.5f;
        Vector3 start = p.transform.position;
        Vector3 end = start + (dir * distance);

        while (timer < duration)
        {
            if (p == null) yield break;
            timer += Time.deltaTime;
            float progress = timer / duration;
            
            // Mouvement vers l'extérieur
            p.transform.position = Vector3.Lerp(start, end, progress);
            
            // La particule devient de plus en plus transparente
            Renderer r = p.GetComponent<Renderer>();
            Color c = r.material.color;
            c.a = 1f - progress;
            r.material.color = c;

            yield return null;
        }
        Destroy(p);
    }
 
    IEnumerator ResolveDuelAnimation(int winnerId, int loserId, Position loserNewPos)
    { 
        // 1. Récupérer les GameObjects des joueurs
        GameObject winnerObj = players[winnerId];
        GameObject loserObj = players[loserId];

        // 2. Récupérer la position de la cellule de duel depuis l'état serveur
        var duelCell = _lastServerState.Players.First(p => p.ID == winnerId);
        Vector3 duelCellPos = gridManager.GetCellWorldPosition(duelCell.Row, duelCell.Col);

        // 3. Animation du gagnant : reste au centre
        float jumpWinDuration = 0.4f;
        float timer = 0f;
        Vector3 startWinPos = winnerObj.transform.position;

        while (timer < jumpWinDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / jumpWinDuration;
            Vector3 currentPos = Vector3.Lerp(startWinPos, duelCellPos, progress);
            float height = Mathf.Sin(progress * Mathf.PI) * 1.5f;
            winnerObj.transform.position = new Vector3(currentPos.x, startWinPos.y + height, currentPos.z);
            yield return null;
        }
        winnerObj.transform.position = duelCellPos;

        // 4. Animation du perdant : expulsion
        Vector3 expulsionWorldPos = gridManager.GetCellWorldPosition(loserNewPos.Row, loserNewPos.Col);
        float slideDuration = 0.3f;
        float t = 0f;
        Vector3 startPos = loserObj.transform.position;

        while (t < 1f)
        {
            t += Time.deltaTime / slideDuration;
            loserObj.transform.position = Vector3.Lerp(startPos, expulsionWorldPos, t);
            yield return null;
        }

        // 5. Appliquer les effets post-duel (dégâts, etc.)
        // Les effets sont déjà dans _lastServerState, tu peux relancer ProcessEffectQueue si besoin
        // if (effectQueue.Count > 0)
        // {
        //     yield return StartCoroutine(ProcessEffectQueue());
        // }
    }


    IEnumerator OnEffectRemoved(int playerId, EffectType effectType, int rank)
    {
        yield return new WaitForSeconds(rank * 0.5f); 
        GameObject playerObj = players[playerId];
        switch(effectType)
        {
            case EffectType.Freeze:
                Debug.Log($"[Anim] Freeze Removed Joueur {playerId+1}");
                Transform cube = playerObj.transform.Find("IceCube_FX");
                if (cube != null) Destroy(cube.gameObject);

                // Remettre la VRAIE couleur du joueur (pas forcément blanc)
                Renderer pRend = playerObj.GetComponent<Renderer>();
                pRend.material.color = GetPlayerColor(playerId); // Utilise ta fonction existante
                break;

            case EffectType.Armor:
                Debug.Log($"[Anim] Armor Removed Joueur {playerId+1}");
                Transform shield = playerObj.transform.Find("Armor_FX");
                if (shield != null) Destroy(shield.gameObject);
                break;
        }
        yield return null; 
    }

    IEnumerator RotateCoin(Transform coinTransform)
    {
        float duration = 5.0f; // La durée du choix du joueur
        float timer = 0f;
        while (timer < duration && coinTransform != null)
        {
            coinTransform.Rotate(Vector3.up * 720f * Time.deltaTime, Space.World);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    void InitializeHealthUI()
    {
        if (playerHealthSliders == null || playerHealthSliders.Length < 4) 
        {
            Debug.LogWarning("Health sliders non assignés");
            return;
        }
        
        for (int i = 0; i < 4; i++)
        {
            if (playerHealthSliders[i] != null)
            {
                playerHealthSliders[i].maxValue = 100;
                playerHealthSliders[i].value = 100; // Mettre à 100% pour tous
                
                // Important : S'assurer que le fill est visible
                if(playerHealthSliders[i].fillRect == null)
                {
                    
                }
                else
                {
                    Image fillImage = playerHealthSliders[i].fillRect.GetComponent<Image>();
                    if (fillImage != null)
                    {
                        fillImage.enabled = true;
                        fillImage.color = GetPlayerColor(i); // Utiliser ta couleur de joueur
                    }
                }
            }
        }
    }

    Color GetPlayerColor(int index)
    {
        Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow };
        return index < colors.Length ? colors[index] : Color.white;
    }
    
    public void UpdatePlayerHealthBar(int playerIndex, int health)
    {
        if (playerIndex < 0 || playerIndex >= playerHealthSliders.Length) return; // Sécurité de l'index
        if (playerHealthSliders[playerIndex] == null) {
            Debug.LogWarning($"Slider UI for player {playerIndex + 1} is not assigned in the Inspector.");
            return;
        }

        Slider targetSlider = playerHealthSliders[playerIndex];

        // Mettre à jour la valeur
        targetSlider.value = health;
        
        // Récupérer l'Image du "fill"
        // 🔥 CORRECTION : On cherche l'Image sur le `fillRect` ou sur un de ses enfants
        Image fillImage = null;
        if (targetSlider.fillRect != null)
        {
            fillImage = targetSlider.fillRect.GetComponent<Image>();
            if (fillImage == null && targetSlider.fillRect.childCount > 0)
            {
                // Tente de trouver l'image sur le premier enfant du fillRect (ex: GameObject "Fill")
                fillImage = targetSlider.fillRect.GetChild(0).GetComponent<Image>();
            }
        }

        if (fillImage == null)
        {
            Debug.LogWarning($"No Image component found on Fill Rect or its first child for player {playerIndex + 1}. Cannot update health bar color/visibility.");
            return; // Ne peut pas continuer sans l'Image
        }

        if (health <= 0)
        {
            fillImage.enabled = false; // Cache la barre de vie
            // Tu peux aussi désactiver le Slider entier ou son GameObject pour un joueur KO
            // targetSlider.gameObject.SetActive(false); 
            
            if (players[playerIndex].activeSelf)
            {
                players[playerIndex].SetActive(false);
            }
        }
        else
        {
            if (!fillImage.enabled) fillImage.enabled = true;
            fillImage.color = GetPlayerColor(playerIndex);
        }
        
    }

    void UpdatePlayerUI(int index, string name, int health, int maxHealth)
    {
        // Adapte selon ta structure UI
        // Exemple si tu as des TextMeshProUGUI :
        playerLabels[index].text = name;
    }

    IEnumerator StartGameAfterGridReady()
    {
        while (gridManager == null) yield return null;

        // Attendre le game_start du serveur
        while (!_gameStartReceived) yield return null;

        ForceCameraPosition();
        SpawnPlayers();
        selectionTimer = selectionTime;
 
        if (timerSlider != null) { timerSlider.maxValue = 1f; timerSlider.value = 1f; }

        if (gameCanvas != null)
            gameCanvas.enabled = true; 

        StartSelectionPhase();
    }
    
    void ForceCameraPosition()
    {
        if (mainCamera == null) return;
        mainCamera.transform.position = new Vector3(0f, cameraHeight, -cameraDistance);
        mainCamera.transform.rotation = Quaternion.Euler(cameraAngle, 0f, 0f);
    }
    

    void SpawnPlayers()
    {
        players = new GameObject[4];
         int[] playerCols = new int[4];
        playerCols[0] = 2;           // 2 cellules du bord gauche
        playerCols[1] = playerCols[0] + 5;  // +4 cellules
        playerCols[2] = playerCols[1] + 5;  // +4 cellules
        playerCols[3] = playerCols[2] + 5;  // +4 cellules = 2 du bord droit (si columns=16)
        for (int i = 0; i < 4; i++)
        {
            Vector3 spawnPos = gridManager.GetCellWorldPosition(0, playerCols[i]);
            players[i] = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            players[i].name = $"Player_{i+1}";
            
            Player playerScript = players[i].GetComponent<Player>();
            if (playerScript == null)
                playerScript = players[i].AddComponent<Player>();
            
            // Assigner l’ID et la vie
            playerScript.playerID = i;
            playerScript.health = 100;
            
            Renderer rend = players[i].GetComponent<Renderer>();
            if (rend != null)
            {
                Color[] colors = { Color.red, Color.blue, Color.green, Color.yellow };
                rend.material.color = colors[i];
            }
        }
        hasDoneSpawnPlayers = true;
    }

    void StartSelectionPhase(GameState state = null)
    {
        isSelectionPhase = true;
        selectionTimer = selectionTime;
        if(state != null)
        {
            foreach (var pState in state.Players)
            {
                if (players[pState.ID] != null && players[pState.ID].activeSelf)
                {
                    Vector2Int current = GetPlayerCurrentCell(pState.ID);
                    playerTargets[pState.ID] = current; // Par défaut: rester sur place
                }
            }
        }

        if (playerControlModes[localPlayerID] == ControlMode.Human 
        && players[localPlayerID] != null
        && (state == null 
            || (state != null && state.Players.Find(p => p.ID == localPlayerID).FreezeTurnsRemaining == 0))) {
            StartCoroutine(ShowHighlightsAfterDelay(0.1f));
        }
    }
    
    public void SetPlayerTarget(int playerIndex, int row, int col)
    {
        // On met à jour l'affichage visuel (ton tableau playerTargets existant)
        playerTargets[playerIndex] = new Vector2Int(row, col);

        // On prépare l'action pour le moteur (on remplace si déjà existante pour ce joueur)
        currentTurnActions.RemoveAll(a => a.PlayerID == playerIndex);
        currentTurnActions.Add(new PlayerAction { 
            PlayerID = playerIndex, 
            TargetRow = row, 
            TargetCol = col 
        });

        Debug.Log($"Cible enregistrée pour Moteur : Joueur {playerIndex + 1} -> ({row},{col})");
    }

    IEnumerator ShowHighlightsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ShowSelectionHighlights();
    }
    
    void ShowSelectionHighlights()
    {
        ClearHighlights();
        
        int idx = localPlayerID; // le joueur contrôlé à la souris
        
        if (idx >= 0 && idx < players.Length && players[idx] != null && players[idx].activeSelf)
        {
            ShowCellsAroundPlayer(players[idx]);
        }
    }
    
    void ShowCellsAroundPlayer(GameObject player)
    {
        Vector2Int playerCell = gridManager.GetCellFromWorldPosition(player.transform.position);
        Debug.Log($"ShowCellsAroundPlayer: joueur en ({playerCell.x},{playerCell.y})");

        var radius = 3; // Rayon de sélection
        if(_lastServerState != null)
        {
            var pState = _lastServerState.Players.FirstOrDefault(p => p.ID == localPlayerID);
            if (pState.MegaJumpTurnsRemaining > 0)
            {
                radius = 8;
            }
        }
        List<Vector2Int> selectableCells = gridManager.GetCellsInRadius(playerCell, radius);
        
        foreach (var cell in selectableCells)
        {
            //Debug.Log($"  - Cellule sélectionnable: ({cell.x},{cell.y})");
            GameObject border = gridManager.ShowCellAsSelectable(cell.x, cell.y);
            highlightedBorders.Add(border);
        }
    }
    
    IEnumerator MoveUpAndDestroy(GameObject obj, bool slow = false)
    {
        float speed = slow ? 1f : 2f;
        float lifetime = slow ? 1.5f : 1f;
        float timer = 0f;

        while (timer < lifetime)
        {
            obj.transform.Translate(Vector3.up * speed * Time.deltaTime);
            timer += Time.deltaTime;
            yield return null;
        }
        Destroy(obj);
    }
    
    void ClearHighlights()
    {
        // Nettoie le tableau dans GridManager
        if (gridManager != null)
        {
            // Méthode à ajouter dans GridManager :
            gridManager.ClearSelectableCells();
        }
        
        foreach (var border in highlightedBorders)
        {
            if (border != null) Destroy(border);
        }
        highlightedBorders.Clear();
    }
    
    void UpdateSelectionTimer()
    {
        selectionTimer = Mathf.Max(0f, selectionTimer - Time.deltaTime);

        if (timerText != null)
            timerText.text = $"CHOISISSEZ ! {Mathf.Max(0, selectionTimer):F1}s";
        
        if (timerSlider != null && !isDuelInProgress)
                timerSlider.value = selectionTimer / selectionTime;

        //if (selectionTimer <= 0) EndSelectionPhase();
         if (selectionTimer <= 0f)
        {
            // Sécurité supplémentaire: vérifier que la grille est prête
            if (!gridManager.IsGridReady()) return;

            while (isDuelInProgress)
            {
                return;
            }
            EndSelectionPhase();
        }
    }
 
    
    void EndSelectionPhase()
    {
        isSelectionPhase = false;
        GameState state = _lastServerState;
        
        // 1. IA : Demander aux IA de remplir leurs PlayerActions
        for (int i = 0; i < players.Length; i++)
        {
            if (playerControlModes[i] == ControlMode.AI && players[i].activeSelf 
            && state.Players.Find(p => p.ID == i).FreezeTurnsRemaining == 0)
            {
                // On utilise ta logique IA actuelle pour obtenir une cible
                Vector2Int aiTarget = DetermineAITarget(i);
                SetPlayerTarget(i, aiTarget.x, aiTarget.y);
            }
        }

         // 2. EXÉCUTER LA LOGIQUE DANS LE MOTEUR
        // C'est ici que le "cerveau" travaille
        //engine.ProcessTurn(currentTurnActions);////    // X XXXXXXXXXXXXXXXX ✅// ✅// ✅ // ✅// ✅// ✅

            // ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅
            if(currentTurnActions.Count > 0)
            {
                SendActionsToServer(currentTurnActions);
            }

            // ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅

        currentTurnActions.Clear(); // On vide pour le prochain tour
 
        // 4. DEMANDER À UNITY D'ANIMER LE RÉSULTAT
        // On utilise les données du moteur pour dire à Unity quoi faire
 
        //StartCoroutine(SyncUnityWithEngine(state));// X XXXXXXXXXXXXXXXX ✅// ✅// ✅ // ✅// ✅// ✅

            // ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅
               // 1. Récupérer la ligne du haut depuis le moteur
            // if(!hasDoneSpawnPlayers ) {
            //     CellEffect[] topRowData = new CellEffect[state.Cols];
            //     for(int c=0; c < state.Cols; c++) {
            //         topRowData[c] = state.Grid[state.Rows - 1, c];
            //     }

            //     CellEffect[] newFutureRowData = state.FutureRow;
            //     if (newFutureRowData == null || newFutureRowData.Length != state.Cols)
            //     {
            //         Debug.LogWarning("Le moteur n'a pas fourni de FutureRow valide. Utilisation d'un tableau vide.");
            //         newFutureRowData = new CellEffect[state.Cols]; // Tableau vide par défaut
            //     }
            //     // 2. Passer cette ligne à Unity
            //     gridManager.InsertRow(topRowData, newFutureRowData);
            // }
                // ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅
    }
    
    // ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅
    private TaskCompletionSource<bool>? _turnCompletion;
    private async void SendActionsToServer(List<PlayerAction> actions)
    {
        _turnCompletion = new TaskCompletionSource<bool>();
        // Convertir les actions en JSON
        var actionList = new List<object>();
        foreach (var action in actions)
        {
            actionList.Add(new {
                playerID = action.PlayerID,
                targetRow = action.TargetRow,
                targetCol = action.TargetCol
            });
        }

        string json = Newtonsoft.Json.JsonConvert.SerializeObject(new {
            op = "submit_actions",
            turn = _lastServerState == null ? 1 : _lastServerState.CurrentTurn + 1,
            actions = actionList
        });

        // Envoyer via WebSocket
        await networkClient.Send(json);
        // await Task.Delay(2000);
            // Attendre la réponse du serveur
        await _turnCompletion.Task;
    }

    // ✅ // ✅// ✅// ✅
   

    public void OnServerMessage(string json)
    {
        Debug.Log("[GameManager] Message reçu: " + json);
        
        // Ici tu parseras le JSON plus tard
        // Pour l'instant, on teste juste la réception
        if (json.Contains("game_start"))
        {
            var root = Newtonsoft.Json.Linq.JObject.Parse(json);
            GameState initialState = Newtonsoft.Json.JsonConvert.DeserializeObject<GameState>(
                root["state"].ToString()
            );

            // Générer la grille Unity à partir de serverState
            gridManager.GenerateGrid(initialState);
            gridManager.CenterGrid();
            gridManager.GenerateFutureRow(initialState.FutureRow);
            string savedNick = PlayerPrefs.GetString("nickname", "");
            localPlayerID = -1;
            
            foreach (var pState in initialState.Players)
            {
                playerTargets[pState.ID] = new Vector2Int(pState.Row, pState.Col);
                if (pState.ID != -1)
                {
                    UpdatePlayerUI(pState.ID, pState.Name, pState.Health, pState.MaxHealth);
                }
                if (pState.Name == savedNick && !pState.IsAI)
                {
                    localPlayerID = pState.ID;
                }
                playerControlModes[pState.ID] = pState.IsAI ? ControlMode.AI : ControlMode.Human;
            }


            _gameStartReceived = true; // Débloquer StartGameAfterGridReady
            _lastServerState = initialState;
 
        }
        else if (json.Contains("turn_result"))
        {
            Debug.Log("[GameManager] Tour reçu !");
            // TODO: Parser l'état et les events, puis appeler SyncUnityWithEngine
            var root = Newtonsoft.Json.Linq.JObject.Parse(json);
            string stateJson = root["state"].ToString();
        
            GameState newState = Newtonsoft.Json.JsonConvert.DeserializeObject<GameState>(stateJson);
            List<GameEventData> events = Newtonsoft.Json.JsonConvert.DeserializeObject<List<GameEventData>>(root["events"].ToString());
            CellEffect[] topRow = Newtonsoft.Json.JsonConvert.DeserializeObject<CellEffect[]>(root["insertedRow"].ToString());
            CellEffect[] futureRow = newState.FutureRow;
            foreach (var pState in newState.Players)
            {
                playerTargets[pState.ID] = new Vector2Int(pState.Row, pState.Col);
            }
             // ✅ Convertir les events serveur en EffectEvent pour ta queue existante
            foreach (var evt in events)
            {
                if(evt.NewHealth == -1)//-1 code dans le NewHealth means "juste remove the effect, no damage number to show" tmp code ?
                {
                    removeEffectQueue.Enqueue(new EffectEvent
                    {
                        playerId = evt.PlayerId,
                        effectType = evt.Type,
                        rank = evt.Rank
                    });
                }
                else
                {
                    effectQueue.Enqueue(new EffectEvent
                    {
                        playerId = evt.PlayerId,
                        launcherPlayerId = evt.LauncherId,
                        effectType = evt.Type,
                        value = evt.Row,
                        rank = evt.Rank,
                        hits = evt.Hits,
                        newHealth = evt.NewHealth,
                        weaponDirection = evt.WeaponDirection,
                        participants = evt.Participants
                    });
                }
            }
            StartCoroutine(SyncUnityWithEngine(newState, topRow, futureRow));
            _turnCompletion?.TrySetResult(true);
            _lastServerState = newState;

        }
        else if(json.Contains("duel_result"))
        {
            var root = Newtonsoft.Json.Linq.JObject.Parse(json);
            int isGold = root["isGold"].ToObject<int>();
            int winnerId = root["winnerId"].ToObject<int>();
            int loserId = root["loserId"].ToObject<int>();
            Position loserNewPos = Newtonsoft.Json.JsonConvert.DeserializeObject<Position>(root["loserNewPos"].ToString());
            GameState newState = Newtonsoft.Json.JsonConvert.DeserializeObject<GameState>(root["state"].ToString());
            List<GameEventData> events = Newtonsoft.Json.JsonConvert.DeserializeObject<List<GameEventData>>(root["events"].ToString());

            effectQueue.Clear();
            foreach (var evt in events)
            {
                effectQueue.Enqueue(new EffectEvent
                {
                    playerId = evt.PlayerId,
                    launcherPlayerId = evt.LauncherId,
                    effectType = evt.Type,
                    value = evt.Row,
                    rank = evt.Rank,
                    hits = evt.Hits,
                    newHealth = evt.NewHealth,
                    weaponDirection = evt.WeaponDirection,
                    participants = evt.Participants
                });
            }

            // Mettre à jour l'état local
            _lastServerState = newState; 
            StartCoroutine(DuelSequence(isGold == 0, winnerId, loserId, loserNewPos)); 
        }
    }

    IEnumerator DuelSequence(bool isGold, int winnerId, int loserId, Position loserNewPos)
    {
        // 1. Spin de la pièce
        yield return StartCoroutine(DuelUIManager.Instance.SpinCoinAndClose(isGold));
        
        // 2. Ensuite seulement : anim du push
        yield return StartCoroutine(ResolveDuelAnimation(winnerId, loserId, loserNewPos));
        
        if (effectQueue.Count > 0)
        {
            yield return StartCoroutine(ProcessEffectQueue());
        }
        // 3. FINI : on peut reset
        isDuelInProgress = false;
        
        // 4. Relancer la phase de sélection
        StartSelectionPhase(_lastServerState);
        //StartCoroutine(SyncUnityWithEngine(newState, topRow, futureRow));
    }

    IEnumerator SyncUnityWithEngine(GameState state, CellEffect[] topRow, CellEffect[] futureRow)
    {
            // 1. Désactiver les choix visuels (cyan/jaune)
        ClearHighlights();

        // 1. Récupérer la ligne du haut depuis le moteur
            // CellEffect[] topRowData = new CellEffect[state.Cols];
            // for(int c=0; c < state.Cols; c++) {
            //     topRowData[c] = state.Grid[state.Rows - 1, c];
            // }

            // CellEffect[] newFutureRowData = state.FutureRow;
            // if (newFutureRowData == null || newFutureRowData.Length != state.Cols)
            // {
            //     Debug.LogWarning("Le moteur n'a pas fourni de FutureRow valide. Utilisation d'un tableau vide.");
            //     newFutureRowData = new CellEffect[state.Cols]; // Tableau vide par défaut
            // }
            // // 2. Passer cette ligne à Unity
            // gridManager.InsertRow(topRowData, newFutureRowData);
            gridManager.InsertRow(topRow, futureRow);

        areJumpsInProgress = true;
        // B. Faire sauter les joueurs vers leurs nouvelles positions calculées par le moteur
        foreach (var pState in state.Players)
        {
            // 1. Vérifier que l'ID est valide
            if (pState.ID < 0 || pState.ID >= players.Length)
            {
                //Debug.LogError($"ID Joueur invalide : {pState.ID}");
                continue; // Passe au joueur suivant
            }

            // 2. Récupérer l'objet visuel correspondant
            GameObject playerObj = players[pState.ID];

            // 3. Vérifier que l'objet existe (il a pu être détruit si le joueur est mort)
            if (playerObj != null && playerObj.activeSelf)
            {
                 // A. Récupérer la cible que le joueur AVAIT choisie
                Vector2Int intendedTarget = playerTargets[pState.ID];
                // B. Récupérer la position où le moteur dit qu'il finit (après ricochet)
                Vector2Int actualTarget = new Vector2Int(pState.Row, pState.Col);

               // --- CAS 1 : LE RICOCHET (Intrus 3 et 4) ---
                if (intendedTarget.x != actualTarget.x || intendedTarget.y != actualTarget.y)
                {
                    StartCoroutine(ReboundAnimation(playerObj, intendedTarget, actualTarget));
                }
                // --- CAS 2 : LE DUEL (Offset de 0.3) ---
                else if (state.PlayerFinalPositions.ContainsKey(pState.ID))
                {
                    var duel = state.CurrentDuels.FirstOrDefault(d => d.PlayerIDs.Contains(pState.ID));
                    int indexInDuel = duel.PlayerIDs.IndexOf(pState.ID);
                    float offset = (indexInDuel == 0) ? -0.3f : 0.3f;
                    
                    Vector3 targetPos = gridManager.GetCellWorldPosition(pState.Row, pState.Col) + Vector3.right * offset;
                    StartCoroutine(JumpToPosition(playerObj, targetPos));
                }
                // --- CAS 3 : SAUT NORMAL ---
                else
                {
                    Vector3 targetPos = gridManager.GetCellWorldPosition(pState.Row, pState.Col);
                    StartCoroutine(JumpToPosition(playerObj, targetPos));
                }
            }
            else if (pState.IsAlive)
            {
                // Cas rare : Le moteur dit qu'il est vivant, mais l'objet n'existe pas.
                Debug.LogWarning($"Le Joueur {pState.ID} est marqué vivant dans le moteur mais son GameObject est manquant !");
            }
        }

        yield return new WaitForSeconds(jumpDuration + 0.1f);
        areJumpsInProgress = false;

        while (isDuelInProgress)
        {
            yield return null;
        }
        
        if (effectQueue.Count > 0)
        {
            yield return StartCoroutine(ProcessEffectQueue());
        }


            // ✅ 5. ATTENDRE QUE LES EFFETS SOIENT FINIS
        while (isProcessingEffects)
        {
            yield return null; // Attend frame par frame
        }
 
        foreach (var pState in state.Players)
        {
            if (!pState.IsAlive && players[pState.ID].activeSelf)
            {
                players[pState.ID].SetActive(false);
            }
        }

            // 5. Vérifier la fin de partie
        int survivors = state.Players.Count(p => p.IsAlive);
        if (survivors <= 1)
        {
            var winner = state.Players.FirstOrDefault(p => p.IsAlive);
            timerText.text = $"FIN ! Vainqueur: Joueur {winner.ID + 1}";
            yield break; // On arrête la boucle du jeu
        }

        if(!isDuelInProgress)
            // D. Relancer le tour suivant
            StartSelectionPhase(state);
    }
    
    IEnumerator ReboundAnimation(GameObject player, Vector2Int intended, Vector2Int actual)
    {
        // 1. Saut vers la case centrale (la cible cliquée)
        Vector3 intendedWorldPos = gridManager.GetCellWorldPosition(intended.x, intended.y);
        yield return StartCoroutine(JumpToPosition(player, intendedWorldPos));

        // 2. Petit temps d'arrêt ou "choc"
        yield return new WaitForSeconds(0.05f);

        // 3. Glissade/Rebond vers la case de destination finale (actual)
        Vector3 actualWorldPos = gridManager.GetCellWorldPosition(actual.x, actual.y);
        float t = 0;
        float bounceDuration = 0.2f;
        Vector3 startPos = player.transform.position;

        while (t < 1f)
        {
            t += Time.deltaTime / bounceDuration;
            // Mouvement linéaire rapide (glissade)
            player.transform.position = Vector3.Lerp(startPos, actualWorldPos, t);
            yield return null;
        }
    }

    IEnumerator JumpToPosition(GameObject player, Vector3 targetPosition)
    {
        Vector3 startPosition = player.transform.position;
        float timer = 0f;
        
            // Garder la même hauteur Y que le départ
        float baseY = startPosition.y;
        targetPosition.y = baseY; // S'assurer que la position cible a la même hauteur

        while (timer < jumpDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / jumpDuration;
            Vector3 horizontalPos = Vector3.Lerp(startPosition, targetPosition, progress);
            float height = Mathf.Sin(progress * Mathf.PI) * jumpHeight;
            player.transform.position = new Vector3(horizontalPos.x, startPosition.y + height, horizontalPos.z);
            yield return null;
        }
        player.transform.position = targetPosition;
    }

    public Vector2Int GetPlayerCurrentCell(int playerIndex)
    {
        if (players == null)
        {
            Debug.LogWarning("⚠️ GetPlayerCurrentCell: players est null");
            return new Vector2Int(-1, -1);
        }
        if (playerIndex < 0 || playerIndex >= players.Length) 
        {
            Debug.LogWarning($"⚠️ PlayerIndex {playerIndex} hors limites");
            return new Vector2Int(-1, -1);
        }
        
        if (players[playerIndex] == null) 
        {
            Debug.LogWarning($"⚠️ Player {playerIndex} est null");
            return new Vector2Int(-1, -1);
        }
        
        if (gridManager == null)
        {
            Debug.LogWarning($"⚠️ GridManager est null");
            return new Vector2Int(-1, -1);
        }
        
        Vector3 playerPos = players[playerIndex].transform.position;
        Debug.Log($"🔍 GetPlayerCurrentCell: Player {playerIndex} position = {playerPos}");
        
        Vector2Int cell = gridManager.GetCellFromWorldPosition(playerPos);
        Debug.Log($"🔍 GetPlayerCurrentCell: résultat = ({cell.x},{cell.y})");
        
        return cell;
    }

    // Dans GameManager.cs
    Vector2Int DetermineAITarget(int playerIndex)//TODO adjust and move the choice in server side later
    {
        // 1. Sécurité : vérifier que le joueur est valide
        if (playerIndex < 0 || playerIndex >= players.Length || players[playerIndex] == null)
        {
            Debug.LogWarning($"⚠️ AI pour joueur {playerIndex} invalide");
            return new Vector2Int(-1, -1);
        }

        Player aiPlayer = players[playerIndex].GetComponent<Player>();
        Vector2Int currentCell = GetPlayerCurrentCell(playerIndex);

        // 2. Récupérer TOUTES les cases atteignables (même rayon 2 que l'humain)
        var radius = 3; // Rayon de sélection par défaut
        if(_lastServerState != null)
        {
            var pState = _lastServerState.Players.FirstOrDefault(p => p.ID == localPlayerID);
            if (pState.MegaJumpTurnsRemaining > 0)
            {
                radius = 8;
            }
        }
        List<Vector2Int> reachableCells = gridManager.GetCellsInRadius(currentCell, radius);

        // 3. Si pas de case atteignable, rester sur place
        if (reachableCells.Count == 0)
        {
            Debug.Log($"🤖 AI Joueur {playerIndex+1} ne peut bouger, reste sur place");
            return currentCell;
        }

        // 4. 🎯 SCORER CHAQUE CASE (plus le score est élevé, mieux c'est)
        Dictionary<Vector2Int, float> cellScores = new Dictionary<Vector2Int, float>();

        foreach (Vector2Int cell in reachableCells)
        {
            float score = 0f;
            int futureRow = cell.x + 1;
            CellEffect effect;
       
            effect = gridManager.GetCellEffect(futureRow, cell.y);

            // RÈGLES DE SCORING MODIFIABLES
            switch (effect.type)
            {
                case EffectType.DamageBomb:
                    score = -1000f; // 🚫 À ÉVITER ABSOLUMENT
                    break;

                case EffectType.Poison:
                    score = -200f; // 🟣 On évite
                    break;

                case EffectType.Freeze:
                    score = -100f; // 🟣 On évite
                    break;

                case EffectType.Armor:
                    if (aiPlayer.health < 50)
                        score = 800f; // Très prioritaire
                    else if (aiPlayer.health < 80 && aiPlayer.health > 50)
                        score = 150f;
                    else
                        score = 50f;
                    break;

                case EffectType.HealthPotion:
                    // 🟢 Si l'IA a peu de PV, la potion vaut beaucoup plus cher
                    if (aiPlayer.health < 50)
                        score = 800f; // Très prioritaire
                    else if (aiPlayer.health < 80 && aiPlayer.health > 50)
                        score = 150f;
                    else
                        score = 50f;
                    break;

                case EffectType.Neutral:
                    // 🟡 Petit bonus si on bouge vers l'avant pour ne pas rester coincé
                    if (cell.x > currentCell.x)
                        score = 50f;
                    else
                        score = 10f;
                    break;

                case EffectType.Missile:
                case EffectType.Laser:
                case EffectType.Spray:
                    score = 75f; // 🟡 On autorise l'IA à prendre le missile pour attaquer les autres
                    break;
            }

            cellScores.Add(cell, score);
        }

        // 5. Choisir la meilleure case (avec un peu de hasard pour ne pas être trop prévisible)
        Vector2Int bestCell = currentCell;
        float bestScore = -9999f;

        // 15% de chance de choisir une case aléatoire parmi les 3 meilleures pour éviter l'IA parfaite
        bool useRandom = Random.value < 0.15f;

        if (useRandom)
        {
            var topCells = cellScores.OrderByDescending(kvp => kvp.Value).Take(3).ToList();
            bestCell = topCells[Random.Range(0, topCells.Count)].Key;
            Debug.Log($"🤖 AI Joueur {playerIndex+1} choisit une case aléatoire pour varier !");
        }
        else
        {
            // Prendre la case avec le score le plus élevé
            foreach (var kvp in cellScores)
            {
                if (kvp.Value > bestScore)
                {
                    bestScore = kvp.Value;
                    bestCell = kvp.Key;
                }
            }
        }
        if (bestScore < 0) bestCell = currentCell;
        Debug.Log($"🤖 AI Joueur {playerIndex+1} choisit ({bestCell.x},{bestCell.y}) | Score: {bestScore}");
        return bestCell;
    }
}