using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;

public class DuelUIManager : MonoBehaviour
{
    public static DuelUIManager Instance;

    public GameObject duelPanel;
    public TextMeshProUGUI timerText;
    public Button goldButton;
    public Button silverButton;
    public Image coinImage; // 🔥 À assigner dans l'Inspector (glisse CoinUI ici)

    private Action<int> onChoiceCallback;
    private bool hasChosen;

    void Awake()
    {
        Instance = this;
        duelPanel.SetActive(false);
        
        // 0 = Or, 1 = Argent
        goldButton.onClick.AddListener(() => MakeChoice(0));
        silverButton.onClick.AddListener(() => MakeChoice(1));
    }

    public void StartDuel(Action<int> callback)
    {
        onChoiceCallback = callback;
        hasChosen = false;
        
        duelPanel.SetActive(true);
        StartCoroutine(RotateCoinWhileWaiting());
        goldButton.gameObject.SetActive(true);
        silverButton.gameObject.SetActive(true);
        coinImage.transform.localScale = Vector3.one;
        coinImage.color = Color.white; // Neutre au début

        StartCoroutine(CountdownRoutine());
    }

    IEnumerator RotateCoinWhileWaiting()
    {
        // rotation visible en UI => axe Z
        while (duelPanel.activeSelf && !hasChosen)
        {
            coinImage.rectTransform.Rotate(0f, 0f, -720f * Time.deltaTime);
            yield return null;
        }
    }

    void MakeChoice(int choice)
    {
        if (hasChosen) return;
        hasChosen = true;
        
        // On cache les boutons dès qu'on a cliqué
        goldButton.gameObject.SetActive(false);
        silverButton.gameObject.SetActive(false);
        
        onChoiceCallback?.Invoke(choice);
    }

    IEnumerator CountdownRoutine()
    {
        float timer = 5f; // 5 secondes
        while (timer > 0 && !hasChosen)
        {
            timerText.text = Mathf.CeilToInt(timer).ToString() + "...";
            timer -= Time.deltaTime;
            yield return null;
        }

        // Si le temps est écoulé, on force le choix Or (0) par défaut
        if (!hasChosen) MakeChoice(0); 
    }

    // Joue l'animation de la pièce et affiche le résultat
    public IEnumerator SpinCoinAndClose(bool goldWins)
    {
        timerText.text = ""; // Cache le timer
        float spinDuration = 2f;
        
        // Effet de rotation (on réduit le scale X de 1 à -1 pour simuler la 3D)
        while (spinDuration > 0)
        {
            float scaleX = Mathf.Cos(spinDuration * 15f); // Vitesse de rotation
            coinImage.transform.localScale = new Vector3(scaleX, 1, 1);
            
            // Alterne les couleurs Or / Argent pendant la rotation
            coinImage.color = (scaleX > 0) ? new Color(1f, 0.8f, 0f) : new Color(0.7f, 0.7f, 0.7f);
            
            spinDuration -= Time.deltaTime;
            yield return null;
        }

        // Arrêt sur la bonne couleur
        coinImage.transform.localScale = Vector3.one;
        coinImage.color = goldWins ? new Color(1f, 0.8f, 0f) : new Color(0.7f, 0.7f, 0.7f);

        goldButton.gameObject.SetActive(goldWins);
        silverButton.gameObject.SetActive(!goldWins);
        yield return new WaitForSeconds(0.6f);

        // On attend un peu pour voir le résultat, puis on ferme
        yield return new WaitForSeconds(1.5f);
        duelPanel.SetActive(false);
    }
}