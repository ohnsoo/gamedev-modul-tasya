using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class TasyaPortalLauncher : MonoBehaviour
{
    private const string DestinationScene = "Activity1Final";
    private Button enterButton;
    private bool isLoading;

    private void Awake()
    {
        enterButton = GetComponent<Button>();
        if (enterButton != null)
        {
            enterButton.onClick.AddListener(EnterMainMap);
        }
    }

    private void OnDestroy()
    {
        if (enterButton != null)
        {
            enterButton.onClick.RemoveListener(EnterMainMap);
        }
    }

    public void EnterMainMap()
    {
        if (isLoading)
        {
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(DestinationScene))
        {
            Debug.LogError("Scene Activity1Final belum ditambahkan ke Build Settings.");
            return;
        }

        isLoading = true;
        SceneManager.LoadScene(DestinationScene, LoadSceneMode.Single);
    }
}