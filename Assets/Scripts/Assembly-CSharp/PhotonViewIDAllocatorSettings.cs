using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

[CreateAssetMenu(fileName = "PhotonViewIDAllocatorSettings", menuName = "ScriptableObjects/PhotonViewIDAllocatorSettings")]
public class PhotonViewIDAllocatorSettings : SerializedScriptableObject
{
	[NonSerialized]
	[OdinSerialize]
	public PathGroup root;
}
