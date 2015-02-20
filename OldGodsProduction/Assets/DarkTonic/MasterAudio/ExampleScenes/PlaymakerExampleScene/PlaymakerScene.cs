using UnityEngine;
using System.Collections;

public class PlaymakerScene : MonoBehaviour {
	void OnGUI() {
		GUI.Label(new Rect(20,40, 450, 160), "Click on the player to play a sound. Move him around before clicking to hear the sound generated from different locations.");
	}
}
