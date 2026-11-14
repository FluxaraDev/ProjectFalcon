using System;
using UnityEngine;
using UnityEngine.UI;

namespace GorillaNetworking
{
	[Serializable]
	public class GorillaText
	{
		[SerializeField]
		private Text text;

		private string failureText;

		private string originalText;

		private bool failedState;

		private Material[] originalMaterials;

		private Material failureMaterial;

		private MeshRenderer meshRenderer;

		public string Text
		{
			get
			{
				return originalText;
			}
			set
			{
				originalText = value;
				if (!failedState)
				{
					text.text = value;
				}
			}
		}

		public void Initialize(MeshRenderer meshRenderer_, Material failureMaterial_)
		{
			meshRenderer = meshRenderer_;
			failureMaterial = failureMaterial_;
			originalMaterials = meshRenderer.materials;
			originalText = text.text;
		}

		public void EnableFailedState(string failText)
		{
			failedState = true;
			text.text = failText;
			failureText = failText;
			Material[] materials = meshRenderer.materials;
			materials[0] = failureMaterial;
			meshRenderer.materials = materials;
		}

		public void DisableFailedState()
		{
			// Project Falcon (2026-11-12): stock code set this flag back
			// to true here, latching every computer text into failed
			// state forever - after the first error, Text writes were
			// stored but never displayed until the next error cycle.
			failedState = false;
			text.text = originalText;
			failureText = "";
			meshRenderer.materials = originalMaterials;
		}
	}
}
