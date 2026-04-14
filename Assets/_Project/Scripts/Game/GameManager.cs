using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using static GridManager;
using UnityEngine.UI;
using System.Linq;
using System.Threading.Tasks;
using TMPro;
using System;

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
    public float selectionTime = 4f;
    
    [Header("Camera")]
    public Camera mainCamera;
    public float cameraHeight = 15f;
    public float cameraDistance = 12f;
    public float cameraAngle = 45f;
    
    [Header("Jump")]
    public float jumpHeight = 1.5f;
    public float jumpDuration = 0.5f;
    
    [Header("UI")]
    public TextMeshProUGUI centralText;
    [SerializeField] 
    private Button exitButton;
    public UnityEngine.UI.Slider timerSlider;
    
    // Variables
    private float rowTimer = 0f;
    private float selectionTimer = 0f;
    private bool isSelectionPhase = false;
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

    private bool _allDuelsResolved = false;
    private Queue<EffectEvent> _duelQueue = new Queue<EffectEvent>();
    private bool _duelQueueRunning = false;
    
    private Action<bool, int, int, Position, int> _pendingDuelResultCallback = null;
    private Dictionary<int, bool> playerSightDisabled = new Dictionary<int, bool>();
    public struct EffectEvent
    {
        public int playerId;
        public int launcherPlayerId;
        public EffectType effectType;
        public int value;//if laser or missile, the row or col. It's special for random cell : value is the final effect type.
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

    IEnumerator ProcessDuelQueue()
    {
        _duelQueueRunning = true;
        
        while (_duelQueue.Count > 0)
        {
            var duelEvt = _duelQueue.Dequeue();
            yield return StartCoroutine(HandleSingleDuel(duelEvt));
        }
        
        yield return new WaitUntil(() => _allDuelsResolved);
        
        if (effectQueue.Count > 0)
            yield return StartCoroutine(ProcessEffectQueue());
        
        _duelQueueRunning = false;
        _allDuelsResolved = false;
        isDuelInProgress = false;
        StartSelectionPhase(_lastServerState);
    }

    IEnumerator HandleSingleDuel(EffectEvent evt)
    {
        int playerId = evt.playerId;
        List<int> participants = evt.participants;
        
        Debug.Log($"⚔️ Duel Visuel lancé ! Participants: {string.Join(", ", participants)}");
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

                yield return null;

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
                    yield return new WaitForSeconds(1f); 
                    duelChoices.Add(participants[0], 0);
                    duelChoices.Add(participants[1], 1);
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(new
                    {
                        op = "duel_choice",
                        duelId = duel.DuelId, // Tu peux utiliser l'index du duel ou un ID serveur
                        playerId = playerId,
                        duelChoices = duelChoices
                    });
                    _ = networkClient.Send(json);

                    
                }

                 bool thisResultReceived = false;
                bool thisIsGold = false;
                int thisWinnerId = -1, thisLoserId = -1, thisHealthBeforeProcess = -1;
                Position thisLoserPos = default;
                
                _pendingDuelResultCallback = (isGold, winnerId, loserId, loserPos, healthBeforeProcess) => {
                    thisIsGold = isGold;
                    thisWinnerId = winnerId;
                    thisLoserId = loserId;
                    thisLoserPos = loserPos;
                    thisHealthBeforeProcess = healthBeforeProcess;
                    thisResultReceived = true;
                };
                
                 yield return new WaitUntil(() => thisResultReceived);

                Destroy(coinFX);

                // Spin + push pour CE duel
                yield return StartCoroutine(DuelUIManager.Instance.SpinCoinAndClose(thisIsGold));
                yield return StartCoroutine(ResolveDuelAnimation(thisWinnerId, thisLoserId, thisLoserPos, thisHealthBeforeProcess));
    }
    
    IEnumerator ProcessEffectQueue()
    {
        isProcessingEffects = true;


        while (effectQueue.Count > 0)
        {
            EffectEvent evt = effectQueue.Dequeue();
            if(evt.effectType == EffectType.CollisionDuel)
            {
                _duelQueue.Enqueue(evt);
                if (!_duelQueueRunning)
                    StartCoroutine(ProcessDuelQueue());
            }
            else
            {
                yield return StartCoroutine(OnEffectAppliedCoroutine(evt.playerId, evt.effectType,
                                                        evt.value, evt.rank, evt.hits, evt.newHealth, evt.weaponDirection, evt.participants));
            }

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
        int launcherCol = -1;
        float mSpeed = 0f;
        float timer = 0f;

        switch(effectType)
        {
            case EffectType.HealthPotion:
                Debug.Log($"Anim Heal Joueur {playerId+1}");
            // Spawn 5 petites sphères vertes aléatoirement autour du joueur
                for (int i = 0; i < 5; i++)
                {
                    GameObject healParticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    healParticle.transform.position = playerObj.transform.position + UnityEngine.Random.insideUnitSphere * 0.5f;
                    healParticle.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                    healParticle.GetComponent<Renderer>().material.color = Color.green;

                    // Faire monter la particule puis la détruire
                    StartCoroutine(MoveUpAndDestroy(healParticle));
                }
                yield return new WaitForSeconds(0.5f);
                UpdatePlayerHealthBar(playerId, newHealth);
                break;

            case EffectType.MegaJump:
                for (int i = 0; i < 5; i++)
                {
                    GameObject healParticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    healParticle.transform.position = playerObj.transform.position + UnityEngine.Random.insideUnitSphere * 0.5f;
                    healParticle.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
                    healParticle.GetComponent<Renderer>().material.color = Color.blue;

                    // Faire monter la particule puis la détruire
                    StartCoroutine(MoveUpAndDestroy(healParticle));
                }
                yield return new WaitForSeconds(0.5f);
                break;

            case EffectType.DamageBomb:
                Debug.Log($"[Anim] Bomb Joueur {playerId+1}");
                // Anim : secousse + flash rouge
                Vector3 originalPos = playerObj.transform.position;
                for (int i = 0; i < 3; i++)
                {
                    playerObj.transform.position += UnityEngine.Random.insideUnitSphere * 0.1f;
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
                            UnityEngine.Random.Range(-0.2f, 0.2f), // Dispersion verticale
                            UnityEngine.Random.Range(-0.8f, 0.8f)  // Dispersion largeur (Z) sur 3 cases
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

            case EffectType.LaserV:
               int laserCol = value; // col du lanceur

                    // 1. Créer le faisceau laser vertical
                    GameObject beamV = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    beamV.GetComponent<Renderer>().material.color = new Color(1f, 0f, 1f, 0.8f); // Magenta pour différencier du H

                    // Positionner au centre de la colonne
                    float gridCenterZ = (gridManager.GetCellWorldPosition(0, laserCol).z + gridManager.GetCellWorldPosition(gridManager.rows - 1, laserCol).z) / 2f;
                    Vector3 beamPosV = new Vector3(playerObj.transform.position.x, playerObj.transform.position.y + 0.5f, gridCenterZ);
                    beamV.transform.position = beamPosV;

                    // Redimensionner pour couvrir toute la hauteur
                    float totalHeight = Mathf.Abs(gridManager.GetCellWorldPosition(gridManager.rows - 1, laserCol).z - gridManager.GetCellWorldPosition(0, laserCol).z);
                    beamV.transform.localScale = new Vector3(0.05f, 0.02f, totalHeight); // X et Z inversés vs horizontal

                    yield return new WaitForSeconds(0.8f);
                    Destroy(beamV);

                    // 2. Mettre à jour les joueurs sur la colonne
                    foreach (var hit in hits)
                    {
                        UpdatePlayerHealthBar(hit.PlayerId, hit.NewHealth);
                    }
                break;

            case EffectType.Missile:
                int mRow = value;
                launcherCol = _lastServerState.Players[playerId].Col;
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
                mSpeed = 15f;
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

             case EffectType.MissileV:
                int mCol = value;
                int launcherRow = _lastServerState.Players[playerId].Row;
                float startZ = playerObj.transform.position.z;

                // --- Trouver les Z d'arrêt pour le visuel ---
                float stopZUp = gridManager.GetCellWorldPosition(0, mCol).z; // row 0 = haut
                float stopZDown = gridManager.GetCellWorldPosition(gridManager.rows - 1, mCol).z; // dernière row = bas

                // On cherche les cibles réelles dans le GameState pour arrêter les missiles dessus
                foreach (var p in _lastServerState.Players)
                {
                    if (p.ID == playerId || !p.IsAlive || p.Col != mCol) continue;
                    float pZ = gridManager.GetCellWorldPosition(p.Row, p.Col).z;
                    // Si le joueur est AU-DESSUS du lanceur, on ajuste stopZUp
                    if (p.Row < launcherRow && pZ < startZ) stopZUp = pZ;
                    // Si le joueur est EN-DESSOUS du lanceur, on ajuste stopZDown
                    if (p.Row > launcherRow && pZ > startZ) stopZDown = pZ;
                }

                // 1. Lancement des deux projectiles
                GameObject mUp = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                mUp.transform.position = playerObj.transform.position + Vector3.up * 0.5f;
                mUp.transform.localScale = new Vector3(0.2f, 0.2f, 0.6f);
                mUp.transform.rotation = Quaternion.Euler(90, 0, 0);
                mUp.GetComponent<Renderer>().material.color = Color.yellow;

                GameObject mDown = Instantiate(mUp, mUp.transform.position, mUp.transform.rotation);

                // 2. Animation de déplacement simultanée
                mSpeed = 15f;
                while (mUp.transform.position.z > stopZUp || mDown.transform.position.z < stopZDown)
                {
                    if (mUp.transform.position.z > stopZUp)
                        mUp.transform.position += Vector3.back * mSpeed * Time.deltaTime; // Vers le haut = Z décroissant

                    if (mDown.transform.position.z < stopZDown)
                        mDown.transform.position += Vector3.forward * mSpeed * Time.deltaTime; // Vers le bas = Z croissant

                    yield return null;
                }

                // 3. Explosion et Update
                Destroy(mUp);
                Destroy(mDown);

                foreach (var hit in hits)
                {
                    UpdatePlayerHealthBar(hit.PlayerId, hit.NewHealth);
                }
                break;

            case EffectType.Lightning:
                // Animation de soulèvement (coroutine rapide)
                StartCoroutine(AnimationHelper.LaunchEffectAnimation(playerObj));

                // 3. Attendre 0.5s pour l'effet de préparation
                yield return new WaitForSeconds(0.5f);

                // 4. Trouver la cible via hits[]
                if (hits == null || hits.Length == 0) yield break;
                var targetHit = hits[0]; // On suppose que hits[0] contient la cible de la foudre
                GameObject targetPlayer = players[targetHit.PlayerId];
                if (targetPlayer == null) yield break;

                // 5. Créer l'éclair vertical (de haut en bas sur la cible)
                GameObject lightningBolt = new GameObject("LightningBolt");
                LineRenderer lineRenderer = lightningBolt.AddComponent<LineRenderer>();
                lineRenderer.material = new Material(Shader.Find("Unlit/Color")) { color = new Color(0.5f, 0.8f, 1f) }; // Bleu électrique
                lineRenderer.startWidth = 0.2f;
                lineRenderer.endWidth = 0.05f;
                lineRenderer.positionCount = 4; // 3 segments pour un éclair en zigzag

                // Positions de l'éclair (zigzag depuis le ciel vers la cible)
                Vector3 startPos = targetPlayer.transform.position + Vector3.up * 5f;
                Vector3 midPos1 = startPos + new Vector3(UnityEngine.Random.Range(-1f, 1f), -2f, UnityEngine.Random.Range(-1f, 1f));
                Vector3 midPos2 = midPos1 + new Vector3(UnityEngine.Random.Range(-1f, 1f), -2f, UnityEngine.Random.Range(-1f, 1f));
                Vector3 endPos = targetPlayer.transform.position;

                lineRenderer.SetPosition(0, startPos);
                lineRenderer.SetPosition(1, midPos1);
                lineRenderer.SetPosition(2, midPos2);
                lineRenderer.SetPosition(3, endPos);

                // Animation de l'éclair (clignotement)
                StartCoroutine(AnimationHelper.LightningFlash(lineRenderer));
                Destroy(lineRenderer.gameObject, 0.8f);

                // 6. Effet d'impact sur la cible (éclat jaune)
                // GameObject hitEffect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                // hitEffect.transform.position = endPos;
                // hitEffect.transform.localScale = Vector3.one * 0.4f;
                // hitEffect.GetComponent<Renderer>().material.color = Color.yellow;
                // Destroy(hitEffect, 0.3f);

                Vector3 pos = targetPlayer.transform.position;
                for (int i = 0; i < 3; i++)
                {
                    targetPlayer.transform.position += UnityEngine.Random.insideUnitSphere * 0.1f;
                    yield return new WaitForEndOfFrame();
                }
                targetPlayer.transform.position = pos;

                // 7. Nettoyage
                Destroy(lightningBolt, 1.0f);
                //Destroy(launchEffect, 0.5f);

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
                    poisonParticle.transform.position = playerObj.transform.position + UnityEngine.Random.insideUnitSphere * 0.5f;
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
                if (!IsPlayerInvisible(playerId) || playerId == localPlayerID)
                {
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
                    timer = 0f;
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
                }
                break;

            case EffectType.Random:
            case EffectType.RandomWeapon:
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

                if(!IsPlayerSightDisabled(playerId))
                {
                    // 2. Changer visuellement la case en dessous du joueur 
                    Vector2Int pPos = GetPlayerCurrentCell(playerId);
                    
                    // ✅ Utiliser la nouvelle fonction du GridManager
                    if (gridManager != null)
                    {
                        gridManager.ForceCellVisual(pPos.x, pPos.y, finalType);
                    }
                    
                    // Petite pause "Révélation" avant que le vrai effet ne frappe
                    yield return new WaitForSeconds(0.5f);
                }
                break;

            case EffectType.Invisibility:
                if (playerId != localPlayerID) // les autres le voient disparaître
                {
                    // Fondu vers transparent
                    yield return StartCoroutine(FadePlayer(playerObj, 1f, 0f, 0.5f));
                }
                else // le joueur lui-même : reste visible mais teinte blanche
                {
                    SetPlayerTint(playerObj, Color.white);
                    // ou une légère transparence pour indiquer l'état
                    //yield return StartCoroutine(FadePlayer(playerObj, 1f, 0.35f, 0.5f));
                }
                break;

            case EffectType.DoubleDamage:
                Debug.Log($"[Anim] DoubleDamage Joueur {playerId+1}");
                    if (!IsPlayerInvisible(playerId) || playerId == localPlayerID)
                    {
                        // 1. Créer un GameObject parent pour regrouper les effets
                        GameObject doubleDamageFX = new GameObject("DoubleDamage_FX");
                        doubleDamageFX.transform.parent = playerObj.transform;
                        doubleDamageFX.transform.localPosition = Vector3.zero;

                        // 2. Ajouter une aura de feu (particules ou halo)
                        GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        aura.transform.parent = doubleDamageFX.transform;
                        aura.transform.localScale = Vector3.one * 1.2f;
                        aura.transform.localPosition = Vector3.zero;

                        // Matériau transparent et orange/rouge
                        Material auraMat = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
                        auraMat.color = new Color(1f, 0.3f, 0f, 0.4f); // Orange transparent
                        aura.GetComponent<Renderer>().material = auraMat;

                        // 3. Ajouter des particules de feu autour du joueur (optionnel mais plus dynamique)
                        for (int i = 0; i < 3; i++)
                        {
                            GameObject fireParticle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                            fireParticle.transform.parent = doubleDamageFX.transform;
                            fireParticle.transform.localPosition = UnityEngine.Random.insideUnitSphere * 0.6f;
                            fireParticle.transform.localScale = Vector3.one * 0.15f;
                            fireParticle.GetComponent<Renderer>().material.color = new Color(1f, 0.5f, 0f); // Orange vif

                            // Animation de mouvement circulaire
                            StartCoroutine(RotateAroundPlayer(fireParticle, doubleDamageFX.transform));
                        }

                        // 4. Animation de pulsation pour l'aura
                        StartCoroutine(PulseAura(aura));
                    }
                break;

            case EffectType.Portal:
            case EffectType.PortalWeapon:
                Debug.Log($"[Anim] Portal Joueur {playerId+1}");
                int newRow = value / gridManager.columns;
                int newCol = value % gridManager.columns;
                // Lancer la téléportation
                yield return StartCoroutine(TeleportPlayer(playerObj, newRow, newCol));
                break;

            case EffectType.SightDisabled:
                Debug.Log($"[Anim] SightDisabled Joueur {playerId+1}");
                StartCoroutine(AnimationHelper.LaunchEffectAnimation(playerObj));
                // player himeself continue to see all, we can add a subtle effect to indicate the blindness
                //todo hide cells for all players except playerId
                if (playerId != localPlayerID)
                {
                    StartCoroutine(gridManager.FlipCellsAnimation(true));//gridManager.HideCellEffects();
                    waitEffectSight = false;
                }
                break;
    
        }
    }

    IEnumerator TeleportPlayer(GameObject playerObj, int newRow, int newCol)
    {
        // 1. Récupérer les positions
        Vector3 oldPosition = playerObj.transform.position;
        Vector3 newPosition = gridManager.GetCellWorldPosition(newRow, newCol);
        newPosition.y = oldPosition.y; // Garder la même hauteur

        // 2. Animation de disparition (clignotement rapide)
        for (int i = 0; i < 3; i++)
        {
            playerObj.GetComponent<Renderer>().enabled = !playerObj.GetComponent<Renderer>().enabled;
            yield return new WaitForSeconds(0.1f);
        }
        playerObj.GetComponent<Renderer>().enabled = true; // Réactiver le rendu

        // 3. Effet de particules "portail" (sans prefab)
        // Créer une sphère transparente pour simuler le portail
        GameObject portalEffect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        portalEffect.transform.position = oldPosition;
        portalEffect.transform.localScale = Vector3.one * 0.8f;
        Material portalMat = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
        portalMat.color = new Color(0.5f, 0f, 1f, 0.5f); // Violet transparent
        portalEffect.GetComponent<Renderer>().material = portalMat;

        // 4. Téléportation (déplacement instantané)
        playerObj.transform.position = newPosition;

        // 5. Effet de réapparition (halo blanc)
        GameObject appearanceEffect = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        appearanceEffect.transform.position = newPosition;
        appearanceEffect.transform.localScale = Vector3.one * 0.6f;
        Material haloMat = new Material(Shader.Find("Legacy Shaders/Transparent/Diffuse"));
        haloMat.color = new Color(1f, 1f, 1f, 0.4f); // Blanc transparent
        appearanceEffect.GetComponent<Renderer>().material = haloMat;

        // 6. Nettoyage (détruire les effets après un délai)
        Destroy(portalEffect, 0.5f);
        Destroy(appearanceEffect, 0.5f);
    }

    bool IsPlayerInvisible(int playerId)
    {
        if (_lastServerState == null) return false;
        var p = _lastServerState.Players.Any(p => p.ID == playerId);
        return p && _lastServerState.Players.First(p => p.ID == playerId).InvisibilityRemaining > 0;
    }

    bool IsPlayerSightDisabled(int playerId)
    {
        if (_lastServerState == null) return false;
        var p = _lastServerState.Players.Any(p => p.ID == playerId);
        return p && _lastServerState.Players.First(p => p.ID == playerId).SightDisabledRemaining > 0;
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
 
    IEnumerator ResolveDuelAnimation(int winnerId, int loserId, Position loserNewPos, int healthBeforeProcess)
    { 
        // 1. Récupérer les GameObjects des joueurs
        GameObject winnerObj = players[winnerId];
        GameObject loserObj = players[loserId];

        // 2. Récupérer la position de la cellule de duel depuis l'état serveur
        var duelCell = _lastServerState.Players.First(p => p.ID == winnerId);
        var row = duelCell.DestRow != -1 ? duelCell.DestRow : duelCell.Row;
        var col = duelCell.DestCol != -1 ? duelCell.DestCol : duelCell.Col;
        Vector3 duelCellPos = gridManager.GetCellWorldPosition(row, col);

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
        UpdatePlayerHealthBar(loserId, healthBeforeProcess);

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
                //if (cube != null) Destroy(cube.gameObject);
                foreach (Transform child in playerObj.transform)
                {
                    if (child.name == "IceCube_FX")
                        Destroy(child.gameObject);
                }
                // Remettre la VRAIE couleur du joueur (pas forcément blanc)
                Renderer pRend = playerObj.GetComponent<Renderer>();
                pRend.material.color = GetPlayerColor(playerId); // Utilise ta fonction existante
                break;

            case EffectType.Armor:
                Debug.Log($"[Anim] Armor Removed Joueur {playerId+1}");
                foreach (Transform child in playerObj.transform)
                {
                    if (child.name == "Armor_FX")
                        Destroy(child.gameObject);
                }
                break;

            case EffectType.Invisibility:
                Color original = playerObj.GetComponent<Player>().originalColor;
                yield return StartCoroutine(FadePlayer(playerObj, 0f, 1f, 0.5f)); // réapparition en fondu
                SetPlayerTint(playerObj, original);
                break;

            case EffectType.DoubleDamage:
                Debug.Log($"[Anim] DoubleDamage Removed Joueur {playerId+1}");
                foreach (Transform child in playerObj.transform)
                {
                    if (child.name == "DoubleDamage_FX")
                        Destroy(child.gameObject);
                }
                break;

            case EffectType.SightDisabled:
                Debug.Log($"[Anim] SightDisabled Removed Joueur {playerId+1}");
                if (playerId == localPlayerID)
                {
                    //gridManager.ShowCellEffects();
                    StartCoroutine(gridManager.FlipCellsAnimation(false));
                }
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

    IEnumerator PulseAura(GameObject aura)
    {
        float pulseDuration = 0.7f;
        Vector3 originalScale = aura.transform.localScale;

        while (true) // Boucle infinie jusqu'à destruction
        {
            float timer = 0f;
            while (timer < pulseDuration)
            {
                float scaleMultiplier = 1f + Mathf.PingPong(timer * 3f, 0.15f);
                aura.transform.localScale = originalScale * scaleMultiplier;
                timer += Time.deltaTime;
                yield return null;
            }
        }
    }

    IEnumerator RotateAroundPlayer(GameObject particle, Transform parent)
    {
        float speed = 2f;
        float radius = 0.6f;

        while (true)
        {
            float angle = Time.time * speed;
            particle.transform.localPosition = new Vector3(
                Mathf.Cos(angle) * radius,
                0f,
                Mathf.Sin(angle) * radius
            );
            yield return null;
        }
    }

    void SetPlayerTint(GameObject obj, Color color)
    {
        Renderer rend = obj.GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = color;
    }

    IEnumerator FadePlayer(GameObject obj, float from, float to, float duration)
    {
        if (obj.transform.Find("Armor_FX") != null)
        {
            Transform armor = obj.transform.Find("Armor_FX");
            Renderer armorRend = armor.GetComponent<Renderer>();
            Color armorColor = armorRend.material.color;
            StartCoroutine(FadeMaterial(armorRend, from, to, duration));
        }
        var renderer = obj.GetComponent<Renderer>();
        float elapsed = 0f;
        Color c = renderer.material.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, elapsed / duration);
            renderer.material.color = c;
            yield return null;
        }
        c.a = to;
        renderer.material.color = c;
    }

    IEnumerator FadeMaterial(Renderer rend, float from, float to, float duration)
    {
        float elapsed = 0f;
        Color c = rend.material.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, elapsed / duration);
            rend.material.color = c;
            yield return null;
        }
        c.a = to;
        rend.material.color = c;
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
                rend.material.SetFloat("_Mode", 2); // 2 = Fade
                rend.material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                rend.material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                rend.material.SetInt("_ZWrite", 0);
                rend.material.DisableKeyword("_ALPHATEST_ON");
                rend.material.EnableKeyword("_ALPHABLEND_ON");
                rend.material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                rend.material.renderQueue = 3000;
                
                rend.material.color = colors[i];
                players[i].GetComponent<Player>().originalColor = colors[i];
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


            // ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅
            //if(currentTurnActions.Count > 0)
            //{
                SendActionsToServer(currentTurnActions);
           // }

            // ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅// ✅ // ✅// ✅// ✅

        currentTurnActions.Clear(); // On vide pour le prochain tour
 
    }
    
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
            Debug.Log("=== MATCH FOUND RECEIVED ===");
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
                if (pState.DestRow != -1 && pState.DestCol != -1)
                    playerTargets[pState.ID] = new Vector2Int(pState.DestRow, pState.DestCol); // jump vers cell portal
                else
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
            
            int healthBeforeProcess = root["loserHealthBeforeProcess"].ToObject<int>();

            effectQueue.Clear();
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

            // Mettre à jour l'état local
            _lastServerState = newState;

            bool allDone = root["allDuelsResolved"].ToObject<bool>();
            if (allDone) _allDuelsResolved = true;

            _pendingDuelResultCallback?.Invoke(isGold == 0, winnerId, loserId, loserNewPos, healthBeforeProcess);
            _pendingDuelResultCallback = null;

            //StartCoroutine(DuelSequence(isGold == 0, winnerId, loserId, loserNewPos)); 
        }
        else if (json.Contains("game_over"))
        {
            var root = Newtonsoft.Json.Linq.JObject.Parse(json);
            string winnerName = root["winnerName"].ToObject<string>();
            
            centralText.gameObject.SetActive(true);
            centralText.text = $"{winnerName} wins !";
            exitButton.gameObject.SetActive(true);
        }
    }
    bool waitEffectSight = true;
    IEnumerator SyncUnityWithEngine(GameState state, CellEffect[] topRow, CellEffect[] futureRow)
    {
        Debug.Log("=== SYNC UNITY START ===");
            // 1. Désactiver les choix visuels (cyan/jaune)
        ClearHighlights();

        playerSightDisabled.Clear();
        foreach (var player in state.Players)
        {
            playerSightDisabled[player.ID] = player.SightDisabledRemaining > 0;
        }

        // if(playerSightDisabled.Count(x=>x.Value == true) == 0)
        // {
        //     waitEffectSight = false;
        // }
        //     yield return new WaitUntil(() => {  return !waitEffectSight; });

        //todo if effectQueue contain an effect SightDisabled for a player, clear playerSightDisabled and wait for the animation to end before continue the sync, same for removeEffectQueue
        // if (effectQueue.Any(e => e.effectType == EffectType.SightDisabled))
        // {
        //         playerSightDisabled.Clear();
        //     //yield return new WaitUntil(() => { return !waitEffectSight; });
        // }

        gridManager.InsertRow(topRow, futureRow, playerSightDisabled);

        if (!effectQueue.Any(e => e.effectType == EffectType.SightDisabled) && 
            playerSightDisabled != null && playerSightDisabled.ContainsKey(localPlayerID) && playerSightDisabled[localPlayerID])
        {
            StartCoroutine(gridManager.FlipCellsAnimation(true, false));
        }
        else if(playerSightDisabled.ContainsKey(localPlayerID) && _lastServerState.CurrentSightDisabled)
        {
            //yield return new WaitForSeconds(0.4f);
            StartCoroutine(gridManager.FlipCellsAnimation(false));
        }
 
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
                    if(pState.DestRow != -1 && pState.DestCol != -1)//case teleport, maybe use PlayerFinalPositions instead ? tmp code
                    {
                        Vector3 targetPos = gridManager.GetCellWorldPosition(pState.DestRow, pState.DestCol);
                        StartCoroutine(JumpToPosition(playerObj, targetPos));
                    }
                    else
                    {
                        StartCoroutine(ReboundAnimation(playerObj, intendedTarget, actualTarget));
                    }
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

    
        var deadLocalPlayer = state.Players.FirstOrDefault(p => p.ID == localPlayerID && !p.IsAlive);
        if (deadLocalPlayer.Name != null)//tmp
        {
            centralText.gameObject.SetActive(true);
            centralText.text = $"you lose";
            exitButton.gameObject.SetActive(true);
            // OnExitClicked();
            // yield break;
        }

        if(!isDuelInProgress)
            // D. Relancer le tour suivant
            StartSelectionPhase(state);
    }
    
    public void OnExitClicked()
    {
        // Compter les humains vivants autres que moi
        int otherHumans = _lastServerState.Players.Count(p => p.ID != localPlayerID && !p.IsAI && p.IsAlive);
        
        if (otherHumans > 0)
        {
            // Il reste des humains — juste quitter sans killer la partie
            _ = networkClient.Send(Newtonsoft.Json.JsonConvert.SerializeObject(new { op = "player_quit", playerId = localPlayerID }));
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
        else
        {
            // Solo ou que des IA restants — kill proprement
            StopAllCoroutines();
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
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
}