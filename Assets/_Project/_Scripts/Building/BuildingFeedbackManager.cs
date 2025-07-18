using System.Collections;
using UnityEngine;

public class BuildingFeedbackManager : MonoBehaviour
{
    public void PlayStartPlacementFeedback()
    {
        // Touch feedback için vibrasyon
        if (SystemInfo.supportsVibration)
        {
            Handheld.Vibrate();
        }
    }
    
    public void PlaySuccessPlacementFeedback()
    {
        // Başarılı yerleştirme feedback'i
        if (SystemInfo.supportsVibration)
        {
            Handheld.Vibrate();
        }
    }
    
    public void PlayInvalidPlacementFeedback()
    {
        // Geçersiz yerleştirme için farklı feedback
        StartCoroutine(InvalidPlacementFeedbackCoroutine());
    }
    
    public void PlayRemoveBuildingFeedback()
    {
        // Silme feedback'i
        if (SystemInfo.supportsVibration)
        {
            Handheld.Vibrate();
        }
    }
    
    private IEnumerator InvalidPlacementFeedbackCoroutine()
    {
        // Kısa vibrasyon pattern'i
        if (SystemInfo.supportsVibration)
        {
            Handheld.Vibrate();
            yield return new WaitForSeconds(0.1f);
            Handheld.Vibrate();
        }
        yield return null;
    }
}