using System.Collections;
using UnityEngine;

public static class AnimationHelper
{
    public static IEnumerator LaunchEffectAnimation(GameObject playerObj)
    {
        float duration = 0.3f;
        float timer = 0f;
        Vector3 startPos = playerObj.transform.position;
        Vector3 endPos = startPos + Vector3.up * 0.5f; // Soulèvement léger

        while (timer < duration)
        {
            playerObj.transform.position = Vector3.Lerp(startPos, endPos, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }

        // Retour à la position initiale
        timer = 0f;
        while (timer < duration)
        {
            playerObj.transform.position = Vector3.Lerp(endPos, startPos, timer / duration);
            timer += Time.deltaTime;
            yield return null;
        }
    }

    public static IEnumerator LightningFlash(LineRenderer lineRenderer)
    {
        float duration = 0.8f;
        float timer = 0f;
        while (timer < duration)
        {
            lineRenderer.startColor = Color.white;
            lineRenderer.endColor = new Color(0.5f, 0.8f, 1f);
            yield return new WaitForSeconds(0.05f);
            if (lineRenderer == null) yield break;

            lineRenderer.startColor = new Color(0.5f, 0.8f, 1f);
            lineRenderer.endColor = Color.white;
            yield return new WaitForSeconds(0.05f);
            timer += 0.1f;
        }
    }
}