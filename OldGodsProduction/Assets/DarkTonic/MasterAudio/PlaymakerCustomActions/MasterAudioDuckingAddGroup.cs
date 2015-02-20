using UnityEngine;
using HutongGames.PlayMaker;
using TooltipAttribute = HutongGames.PlayMaker.TooltipAttribute;

[ActionCategory(ActionCategory.Audio)]
[Tooltip("Add a Sound Group in Master Audio to the list of sounds that cause music ducking.")]
public class MasterAudioDuckingAddGroup : FsmStateAction {
    [RequiredField]
    [Tooltip("Name of Master Audio Sound Group")]
	public FsmString soundGroupName;
	
	[RequiredField]
	[HasFloatSlider(0, 1)]
    [Tooltip("Percentage of sound played to start unducking")]
	public FsmFloat beginUnduck;
		
	
	public override void OnEnter() {
		MasterAudio.AddSoundGroupToDuckList(soundGroupName.Value, beginUnduck.Value);
		
		Finish();
	}
	
	public override void Reset() {
		soundGroupName = new FsmString(string.Empty);
		var defaultRise = .5f;
			
		var ma = MasterAudio.Instance;
		if (ma != null) {
			defaultRise = ma.defaultRiseVolStart;
		}
			
		beginUnduck = new FsmFloat(defaultRise);
	}
}
