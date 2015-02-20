using UnityEngine;
using HutongGames.PlayMaker;
using TooltipAttribute = HutongGames.PlayMaker.TooltipAttribute;

[ActionCategory(ActionCategory.Audio)]
[Tooltip("Change the pich of a variation (or all variations) in a Sound Group in Master Audio")]
public class MasterAudioVariationChangePitch : FsmStateAction {
	private Transform trans;
	
    [RequiredField]
    [Tooltip("Name of Master Audio Sound Group")]
	public FsmString soundGroupName;

    [Tooltip("Name of specific variation (optional)")]
	public FsmString variationName;

    [RequiredField]
	public FsmBool changeAllVariations = new FsmBool(false);

	[HasFloatSlider(-3,3)]
	public FsmFloat pitch = new FsmFloat(1f);

    [Tooltip("Repeat every frame while the state is active.")]
    public bool everyFrame;
	
	public override void OnEnter() {
		ChangePitch();
		
		if (!everyFrame) {
			Finish();
		}
	}
	
	public override void OnUpdate() {
		ChangePitch();
	}
	
	private void ChangePitch() {
		if (trans == null) {
			trans = Owner.transform;
		}
		
		var groupName = soundGroupName.Value;
		var childName = variationName.Value;
		
		if (string.IsNullOrEmpty(childName)) {
			childName = null;
		}
		
		MasterAudio.ChangeVariationPitch(groupName, changeAllVariations.Value, childName, pitch.Value);
	}
	
	public override void Reset() {
		soundGroupName = new FsmString(string.Empty);
		variationName = new FsmString(string.Empty);
		changeAllVariations = new FsmBool(false);
		pitch = new FsmFloat(1f);
	}
}
