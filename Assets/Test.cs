using System.Collections;
using System.Collections.Generic;
using RenderHeads.Media.AVProDeckLink;
using UnityEngine;

public class Test : MonoBehaviour
{

    // Start is called before the first frame update
    private void Start()
    {

        var a = gameObject.AddComponent<DeckLinkInput>();
        
        a.DeviceIndex = 0;

    }

}
