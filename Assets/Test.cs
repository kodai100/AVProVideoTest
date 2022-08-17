using System.Collections;
using System.Collections.Generic;
using RenderHeads.Media.AVProDeckLink;
using UnityEngine;

public class Test : MonoBehaviour
{

    [SerializeField] private DeckLinkInput decklinkInput;
    
    // Start is called before the first frame update
    private void Start()
    {


        decklinkInput.DeviceIndex = 0;

    }

}
