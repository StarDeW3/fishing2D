using UnityEngine;
using TMPro;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedTMPText : MonoBehaviour
{
    [SerializeField] private string key;
    [TextArea] [SerializeField] private string fallback;

    private TMP_Text tmp;

    private void Awake()
    {
        tmp = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (LocalizationManager.instance != null)
            LocalizationManager.instance.LanguageChanged += Refresh;

        Refresh();
    }

    private void OnDisable()
    {
        if (LocalizationManager.instance != null)
            LocalizationManager.instance.LanguageChanged -= Refresh;
    }

    public void SetKey(string newKey, string newFallback = null)
    {
        key = newKey;
        if (newFallback != null) fallback = newFallback;
        Refresh();
    }

    private void Refresh()
    {
        if (tmp == null) tmp = GetComponent<TMP_Text>();
        if (tmp == null) return;

        tmp.text = LocalizationManager.T(key, fallback);
    }
}
