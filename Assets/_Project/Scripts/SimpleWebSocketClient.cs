using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

public class SimpleWebSocketClient : MonoBehaviour
{
    public string url = "ws://localhost:5000/ws";
    ///public string url = "ws://192.168.1.16:5000/ws";
    public GameManager gameManager;
    private ClientWebSocket _ws;
    private bool _isConnected = false;
    private CancellationTokenSource _cts;

    void Start()
    {
    }

    public async void ConnectAndJoin(string mode, string playerName)
    {
        if (_ws != null && _ws.State == WebSocketState.Open)
        {
            Debug.Log("✅ Déjà connecté, envoi du message direct.");
            // Envoie juste le message, pas de nouvelle connexion
            object msg = (mode == "join") 
                ? new { op = "join", playerName = playerName }
                : new { op = "join_queue", playerName = playerName };
                
            await Send(Newtonsoft.Json.JsonConvert.SerializeObject(msg));
            return; 
        }
        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        try
        {
            Debug.Log("Connexion au serveur...");
            await _ws.ConnectAsync(new Uri(url), _cts.Token);
            Debug.Log("✅ Connecté !");

            object msg;
            
            string name = PlayerPrefs.GetString("nickname", "Player1");
            if (mode == "join")
            {
                msg = new { op = "join", playerName = playerName };
            }
            else // queue
            {
                msg = new { op = "join_queue", playerName = playerName };
            }
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
            await Send(json);

            _ = ReceiveLoop(); // Lance l'écoute
        }
        catch (Exception e)
        {
            Debug.LogError($"Erreur : {e.Message}");
        }
    }

    public void AgreeFillAI()
    {
        if (_ws?.State != WebSocketState.Open) return;
        
        var msg = new { op = "agree_fill_ai" };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
        _ = Send(json);
        
        Debug.Log("✅ J'ai cliqué sur 'Avec IA'");
    }
    
    public async void CancelQueue()
    {
        if (_ws?.State != WebSocketState.Open) return;
        
        var msg = new { op = "cancel_queue" };
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(msg);
        await Send(json);
    }

    async Task ReceiveLoop()
    {
        var buffer = new byte[1024 * 4];

        while (_ws.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
        {
            try
            {
                var sb = new StringBuilder();

                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        Debug.Log("Déconnecté par le serveur");
                        return;
                    }

                    if (result.MessageType != WebSocketMessageType.Text)
                        continue;

                    sb.Append(Encoding.UTF8.GetString(buffer, 0, result.Count));

                } while (!result.EndOfMessage);

                string msg = sb.ToString();
                Debug.Log("📩 Reçu complet: " + msg);

                GameManager gm = FindObjectOfType<GameManager>();
                if (gm != null)
                    gm.OnServerMessage(msg);

                MainMenuManager mmm = FindObjectOfType<MainMenuManager>();
                if (mmm != null)
                    mmm.OnServerMessage(msg);
            }
            catch (Exception e)
            {
                Debug.LogError("Erreur réception: " + e.Message);
                break;
            }
        }
    }

    public async Task Send(string message)
    {
        if (_ws != null && _ws.State == WebSocketState.Open)
        {
            var bytes = Encoding.UTF8.GetBytes(message);
            await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }
    }

    void OnDestroy()
    {
        _cts?.Cancel();
        _ws?.Dispose();
    }

    void OnApplicationQuit()
    {
        _cts?.Cancel();
        _ws?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Quit", CancellationToken.None);
    }
}