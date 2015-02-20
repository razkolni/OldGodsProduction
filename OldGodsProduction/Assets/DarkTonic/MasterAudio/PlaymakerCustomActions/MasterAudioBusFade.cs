using UnityEngine;
using HutongGames.PlayMaker;
using TooltipAttribute = HutongGames.PlayMaker.TooltipAttribute;

[ActionCategory(ActionCategory.Audio)]
[Tooltip("Fade a Bus in Master Audio to a specific volume over X seconds.")]
public class MasterAudioBusFade : FsmStateAction {
	[Tooltip("Check this to perform action on all Buses")]
	public FsmBool allBuses;	
	
    [Tooltip("Name of Master Audio Bus")]
	public FsmString busName;

    [RequiredField]
	[HasFloatSlider(0, 1)]
	[Tooltip("Target Bus volume")]
	public FsmFloat targetVolume;
	
    [RequiredField]
	[HasFloatSlider(0, 10)]
	[Tooltip("Amount of time to complete fade (seconds)")]
	public FsmFloat fadeTime;
	
	public override void OnEnter() {
		if (!allBuses.Value && string.IsNullOrEmpty(busName.Value)) {
			Debug.LogError("You must either check 'All Buses' or enter the Bus Name");
			return;
		}
		
		if (allBuses.Value) {
			var busNames = MasterAudio.RuntimeBusNames;
			for (var i = 0; i < busNames.Count; i++) {
				MasterAudio.FadeBusToVolume(busNames[i], targetVolume.Value, fadeTime.Value);
			}
		} else {
			MasterAudio.FadeBusToVolume(busName.Value, targetVolume.Value, fadeTime.Value);
		}
		
		Finish();
	}
	
	public override void Reset() {
		allBuses = new FsmBool(false);
		busName = new FsmString(string.Empty);
		targetVolume = new FsmFloat();
		fadeTime = new FsmFloat();
	}
}
