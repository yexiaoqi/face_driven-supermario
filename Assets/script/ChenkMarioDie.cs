using UnityEngine;
using System.Collections;

public class ChenkMarioDie : MonoBehaviour {

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
        if (transform.position.y < -1.1f)
        {
            setMarioDie();
        }
	}


    void OnGUI()
    {

    }

    public void setMarioDie()
    {
        World.playAudio(World.dead1AudioIndex);
        //transform.position = new Vector3(-1, 0, 0);
        //Camera.main.transform.position = new Vector3(0, 0, -10);
#pragma warning disable CS0618 // 类型或成员已过时
        Application.LoadLevel("1");
#pragma warning restore CS0618 // 类型或成员已过时
    }
}
