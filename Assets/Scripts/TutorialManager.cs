using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public bool hasTutorialKey = false;

    public static TutorialManager Instance;

    void Awake()
    {
        Instance = this;
    }
}
