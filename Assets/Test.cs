using RenderHeads.Media.AVProDeckLink;
using UnityEngine;

public class Test : MonoBehaviour
{

    [SerializeField] private int mode;

    [SerializeField] private IMGUIDisplay display;
    
    private DeckLinkInput a;

    // Start is called before the first frame update
    private void Start()
    {

        a = gameObject.AddComponent<DeckLinkInput>();

        display._inputDecklink = a;

    }

    private int prevMode;
    
    private void Update()
    {

        mode = Mathf.FloorToInt(Mathf.Max((float)mode, -1f));
        
        if (prevMode != mode)
        {
            a.ModeIndex = mode;
            a.Begin();
        }

        prevMode = mode;

    }

}
