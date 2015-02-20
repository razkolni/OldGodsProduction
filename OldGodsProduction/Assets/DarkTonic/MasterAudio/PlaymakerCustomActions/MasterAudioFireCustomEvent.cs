using UnityEngine;
using HutongGames.PlayMaker;
using TooltipAttribute = HutongGames.PlayMaker.TooltipAttribute;

[ActionCategory(ActionCategory.Audio)]
[Tooltip("Fire a Custom Event in Master Audio")]
public class MasterAudioFireCustomEvent : FsmStateAction {
	[RequiredField]
    [Tooltip("The Custom Event (defined in Master Audio prefab) to fire.")]
    public FsmString customEventName;
	
    [Tooltip("The origin point of the Custom Event")]	
	public FsmVector3 eventOriginPoint;
	
	public override void OnEnter() {
		MasterAudio.FireCustomEvent(customEventName.Value, eventOriginPoint.Value);
		
		Finish();
	}
	
	public override void Reset() {
		customEventName = new FsmString(string.Empty);
		eventOriginPoint = new FsmVector3();
	}
}
