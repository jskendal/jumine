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
    public GameManager gameManager;
    private ClientWebSocket _ws;
    private bool _isConnected = false;
    private CancellationTokenSource _cts;

    void Start()
    {
        //Connect();
    }

    async void Connect()
    {
        _ws = new ClientWebSocket();
        _cts = new CancellationTokenSource();

        try
        {
            Debug.Log("Tentative de connexion à " + url);
            await _ws.ConnectAsync(new Uri(url), _cts.Token);
            _isConnected = true;
            Debug.Log("✅ CONNECTÉ AU SERVEUR !");

            // Envoyer un message de join
            await Send("{\"op\":\"join\"}");

            // Lancer la boucle de réception
            _ = ReceiveLoop();
        }
        catch (Exception e)
        {
            Debug.LogError("❌ Échec connexion: " + e.Message);
        }
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

                if (gameManager != null)
                    gameManager.OnServerMessage(msg);
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

    void OnApplicationQuit()
    {
        _cts?.Cancel();
        _ws?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Quit", CancellationToken.None);
    }
}