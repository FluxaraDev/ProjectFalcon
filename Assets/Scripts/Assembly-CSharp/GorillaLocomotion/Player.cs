using System;
using System.Collections.Generic;
using UnityEngine;

namespace GorillaLocomotion
{
	public class Player : MonoBehaviour
	{
		[Serializable]
		public struct MaterialData
		{
			public string matName;

			public bool overrideAudio;

			public AudioClip audio;

			public bool overrideSlidePercent;

			public float slidePercent;
		}

		private static Player _instance;

		public static bool hasInstance;

		public SphereCollider headCollider;

		public CapsuleCollider bodyCollider;

		private float bodyInitialRadius;

		private float bodyInitialHeight;

		private RaycastHit bodyHitInfo;

		private RaycastHit lastHitInfoHand;

		public Transform leftHandFollower;

		public Transform rightHandFollower;

		public Transform rightHandTransform;

		public Transform leftHandTransform;

		private Vector3 lastLeftHandPosition;

		private Vector3 lastRightHandPosition;

		public Vector3 lastHeadPosition;

		private Rigidbody playerRigidBody;

		public int velocityHistorySize;

		public float maxArmLength = 1f;

		// Project Falcon: long arms. Multiplies the head->hand offset
		// BEFORE the maxArmLength clamp - raising the cap alone does
		// nothing, the physical hand never reaches it. 1 = stock reach.
		// Set by FalconPlayerOptions (computer ITEMS page, option 1).
		public static float FalconArmScale = 1f;

		private Vector3 ScaledHandPosition(Transform handTransform, Vector3 handOffset)
		{
			Vector3 raw = PositionWithOffset(handTransform, handOffset);
			if (FalconArmScale == 1f)
			{
				return raw;
			}
			return headCollider.transform.position + (raw - headCollider.transform.position) * FalconArmScale;
		}


		public float unStickDistance = 1f;

		public float velocityLimit;

		public float slideVelocityLimit;

		public float maxJumpSpeed;

		public float jumpMultiplier;

		public float minimumRaycastDistance = 0.05f;

		public float defaultSlideFactor = 0.03f;

		public float slidingMinimum = 0.9f;

		public float defaultPrecision = 0.995f;

		public float teleportThresholdNoVel = 1f;

		public float frictionConstant = 1f;

		public float slideControl = 0.00425f;

		public float stickDepth = 0.01f;

		private Vector3[] velocityHistory;

		private Vector3[] slideAverageHistory;

		private int velocityIndex;

		public Vector3 currentVelocity;

		private Vector3 denormalizedVelocityAverage;

		private Vector3 lastPosition;

		public Vector3 rightHandOffset;

		public Vector3 leftHandOffset;

		public Vector3 bodyOffset;

		public LayerMask locomotionEnabledLayers;

		public bool wasLeftHandTouching;

		public bool wasRightHandTouching;

		public bool wasHeadTouching;

		public int currentMaterialIndex;

		public bool leftHandSlide;

		public Vector3 leftHandSlideNormal;

		public bool rightHandSlide;

		public Vector3 rightHandSlideNormal;

		public Vector3 headSlideNormal;

		public float rightHandSlipPercentage;

		public float leftHandSlipPercentage;

		public float headSlipPercentage;

		public bool wasLeftHandSlide;

		public bool wasRightHandSlide;

		public Vector3 rightHandHitPoint;

		public Vector3 leftHandHitPoint;

		public float scale = 1f;

		public bool debugMovement;

		public bool disableMovement;

		public bool inOverlay;

		public bool didATurn;

		public GameObject turnParent;

		public int leftHandMaterialTouchIndex;

		public GorillaSurfaceOverride leftHandSurfaceOverride;

		public int rightHandMaterialTouchIndex;

		public GorillaSurfaceOverride rightHandSurfaceOverride;

		public GorillaSurfaceOverride currentOverride;

		public List<MaterialData> materialData;

		private bool leftHandColliding;

		private bool rightHandColliding;

		private bool headColliding;

		private Vector3 finalPosition;

		private Vector3 rigidBodyMovement;

		private Vector3 firstIterationLeftHand;

		private Vector3 firstIterationRightHand;

		private Vector3 firstIterationHead;

		private RaycastHit hitInfo;

		private RaycastHit iterativeHitInfo;

		private RaycastHit collisionsInnerHit;

		private float slipPercentage;

		private Vector3 bodyOffsetVector;

		private Vector3 distanceTraveled;

		private Vector3 movementToProjectedAboveCollisionPlane;

		private MeshCollider meshCollider;

		private Mesh collidedMesh;

		private MaterialData foundMatData;

		private string findMatName;

		private int vertex1;

		private int vertex2;

		private int vertex3;

		private List<int> trianglesList = new List<int>(1000000);

		private List<Material> materialsList = new List<Material>(50);

		private Dictionary<Mesh, int[]> meshTrianglesDict = new Dictionary<Mesh, int[]>();

		private int[] sharedMeshTris;

		private float lastRealTime;

		private float calcDeltaTime;

		private float tempRealTime;

		private Vector3 junkNormal;

		private Vector3 slideAverage;

		private Vector3 slideAverageNormal;

		private Vector3 tempVector3;

		private RaycastHit tempHitInfo;

		private RaycastHit junkHit;

		private Vector3 firstPosition;

		private RaycastHit tempIterativeHit;

		private bool collisionsReturnBool;

		private float overlapRadiusFunction;

		private float maxSphereSize1;

		private float maxSphereSize2;

		private Collider[] overlapColliders = new Collider[10];

		private int overlapAttempts;

		private int touchPoints;

		private float averageSlipPercentage;

		private Vector3 surfaceDirection;

		public float debugMagnitude;

		public float iceThreshold = 0.9f;

		private float bodyMaxRadius;

		public float bodyLerp = 0.17f;

		private bool areBothTouching;

		private float slideFactor;

		public bool didAJump;

		private Renderer slideRenderer;

		private RaycastHit[] rayCastNonAllocColliders;

		private Vector3[] crazyCheckVectors;

		private RaycastHit emptyHit;

		private int bufferCount;

		private Vector3 lastOpenHeadPosition;

		private List<Material> tempMaterialArray = new List<Material>();

		private int disableGripFrameIdx = -1;

		private Platform currentPlatform;

		private Platform lastPlatformTouched;

		private Vector3 currentFrameTouchPos;

		private Vector3 lastFrameTouchPosLocal;

		private Vector3 lastFrameTouchPosWorld;

		private Vector3 refMovement = Vector3.zero;

		private Vector3 platformTouchOffset;

		private Vector3 debugLastRightHandPosition;

		private Vector3 debugPlatformDeltaPosition;

		[SerializeField]
		private ConnectedControllerHandler controllerState;

		public int sizeLayerMask;

		public static Player Instance => _instance;

		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				UnityEngine.Object.Destroy(base.gameObject);
			}
			else
			{
				_instance = this;
				hasInstance = true;
			}
			InitializeValues();
			playerRigidBody.maxAngularVelocity = 0f;
			bodyOffsetVector = new Vector3(0f, (0f - bodyCollider.height) / 2f, 0f);
			bodyInitialHeight = bodyCollider.height;
			bodyInitialRadius = bodyCollider.radius;
			rayCastNonAllocColliders = new RaycastHit[5];
			crazyCheckVectors = new Vector3[7];
			emptyHit = default(RaycastHit);
			crazyCheckVectors[0] = Vector3.up;
			crazyCheckVectors[1] = Vector3.down;
			crazyCheckVectors[2] = Vector3.left;
			crazyCheckVectors[3] = Vector3.right;
			crazyCheckVectors[4] = Vector3.forward;
			crazyCheckVectors[5] = Vector3.back;
			crazyCheckVectors[6] = Vector3.zero;
			if (controllerState == null)
			{
				controllerState = GetComponent<ConnectedControllerHandler>();
			}
		}

		protected void OnDestroy()
		{
			if (_instance == this)
			{
				_instance = null;
				hasInstance = false;
			}
		}

		public void InitializeValues()
		{
			Physics.SyncTransforms();
			playerRigidBody = GetComponent<Rigidbody>();
			velocityHistory = new Vector3[velocityHistorySize];
			slideAverageHistory = new Vector3[velocityHistorySize];
			for (int i = 0; i < velocityHistory.Length; i++)
			{
				velocityHistory[i] = Vector3.zero;
				slideAverageHistory[i] = Vector3.zero;
			}
			leftHandFollower.transform.position = leftHandTransform.position;
			rightHandFollower.transform.position = rightHandTransform.position;
			lastLeftHandPosition = leftHandFollower.transform.position;
			lastRightHandPosition = rightHandFollower.transform.position;
			lastHeadPosition = headCollider.transform.position;
			wasLeftHandTouching = false;
			wasRightHandTouching = false;
			velocityIndex = 0;
			denormalizedVelocityAverage = Vector3.zero;
			slideAverage = Vector3.zero;
			lastPosition = base.transform.position;
			lastRealTime = Time.realtimeSinceStartup;
			lastOpenHeadPosition = headCollider.transform.position;
			bodyCollider.transform.position = PositionWithOffset(headCollider.transform, bodyOffset) + bodyOffsetVector;
			bodyCollider.transform.eulerAngles = new Vector3(0f, headCollider.transform.eulerAngles.y, 0f);
		}

		public void FixedUpdate()
		{
			AntiTeleportTechnology();
			if (scale != 1f)
			{
				playerRigidBody.AddForce(-Physics.gravity * (1f - scale), ForceMode.Acceleration);
			}
		}

		private void BodyCollider()
		{
			if (MaxSphereSizeForNoOverlap(bodyInitialRadius * scale, PositionWithOffset(headCollider.transform, bodyOffset), ignoreOneWay: false, out bodyMaxRadius))
			{
				if (scale > 0f)
				{
					bodyCollider.radius = bodyMaxRadius / scale;
				}
				if (Physics.SphereCast(PositionWithOffset(headCollider.transform, bodyOffset), bodyMaxRadius, Vector3.down, out bodyHitInfo, bodyInitialHeight * scale - bodyMaxRadius, locomotionEnabledLayers))
				{
					bodyCollider.height = (bodyHitInfo.distance + bodyMaxRadius) / scale;
				}
				else
				{
					bodyCollider.height = bodyInitialHeight;
				}
				if (!bodyCollider.gameObject.activeSelf)
				{
					bodyCollider.gameObject.SetActive(value: true);
				}
			}
			else
			{
				bodyCollider.gameObject.SetActive(value: false);
			}
			bodyCollider.height = Mathf.Lerp(bodyCollider.height, bodyInitialHeight, bodyLerp);
			bodyCollider.radius = Mathf.Lerp(bodyCollider.radius, bodyInitialRadius, bodyLerp);
			bodyOffsetVector = Vector3.down * bodyCollider.height / 2f;
			bodyCollider.transform.position = PositionWithOffset(headCollider.transform, bodyOffset) + bodyOffsetVector * scale;
			bodyCollider.transform.eulerAngles = new Vector3(0f, headCollider.transform.eulerAngles.y, 0f);
		}

		private Vector3 CurrentHandPosition(Transform handTransform, Vector3 handOffset)
		{
			if (inOverlay)
			{
				return headCollider.transform.position + headCollider.transform.up * -0.5f * scale;
			}
			if ((ScaledHandPosition(handTransform, handOffset) - headCollider.transform.position).magnitude < maxArmLength * scale)
			{
				return ScaledHandPosition(handTransform, handOffset);
			}
			return headCollider.transform.position + (ScaledHandPosition(handTransform, handOffset) - headCollider.transform.position).normalized * maxArmLength * scale;
		}

		private Vector3 LastLeftHandPosition()
		{
			return lastLeftHandPosition + MovingSurfaceMovement();
		}

		private Vector3 LastRightHandPosition()
		{
			return lastRightHandPosition + MovingSurfaceMovement();
		}

		private Vector3 CurrentLeftHandPosition()
		{
			if (inOverlay)
			{
				return headCollider.transform.position + headCollider.transform.up * -0.5f * scale;
			}
			if ((ScaledHandPosition(leftHandTransform, leftHandOffset) - headCollider.transform.position).magnitude < maxArmLength * scale)
			{
				return ScaledHandPosition(leftHandTransform, leftHandOffset);
			}
			return headCollider.transform.position + (ScaledHandPosition(leftHandTransform, leftHandOffset) - headCollider.transform.position).normalized * maxArmLength * scale;
		}

		private Vector3 CurrentRightHandPosition()
		{
			if (inOverlay)
			{
				return headCollider.transform.position + headCollider.transform.up * -0.5f * scale;
			}
			if ((ScaledHandPosition(rightHandTransform, rightHandOffset) - headCollider.transform.position).magnitude < maxArmLength * scale)
			{
				return ScaledHandPosition(rightHandTransform, rightHandOffset);
			}
			return headCollider.transform.position + (ScaledHandPosition(rightHandTransform, rightHandOffset) - headCollider.transform.position).normalized * maxArmLength * scale;
		}

		private Vector3 PositionWithOffset(Transform transformToModify, Vector3 offsetVector)
		{
			return transformToModify.position + transformToModify.rotation * offsetVector * scale;
		}

		private void LateUpdate()
		{
			Vector3 position = headCollider.transform.position;
			turnParent.transform.localScale = Vector3.one * scale;
			playerRigidBody.MovePosition(playerRigidBody.position + position - headCollider.transform.position);
			Camera.main.nearClipPlane = ((scale > 0.5f) ? 0.01f : 0.002f);
			Camera.main.farClipPlane = ((scale > 0.5f) ? 500f : 15f);
			Physics.SyncTransforms();
			debugLastRightHandPosition = lastRightHandPosition;
			debugPlatformDeltaPosition = MovingSurfaceMovement();
			rigidBodyMovement = Vector3.zero;
			firstIterationLeftHand = Vector3.zero;
			firstIterationRightHand = Vector3.zero;
			firstIterationHead = Vector3.zero;
			currentFrameTouchPos = ComputeWorldHitPoint(lastHitInfoHand, lastFrameTouchPosLocal);
			if (debugMovement)
			{
				tempRealTime = Time.time;
				calcDeltaTime = Time.deltaTime;
				lastRealTime = tempRealTime;
			}
			else
			{
				tempRealTime = Time.realtimeSinceStartup;
				calcDeltaTime = tempRealTime - lastRealTime;
				lastRealTime = tempRealTime;
				if (calcDeltaTime > 0.1f)
				{
					calcDeltaTime = 0.05f;
				}
			}
			if (lastPlatformTouched != null)
			{
				refMovement = currentFrameTouchPos - lastFrameTouchPosWorld;
			}
			if (!didAJump && (wasLeftHandTouching || wasRightHandTouching))
			{
				base.transform.position = base.transform.position + 4.9f * Vector3.down * calcDeltaTime * calcDeltaTime * scale;
				if (Vector3.Dot(denormalizedVelocityAverage, slideAverageNormal) <= 0f && Vector3.Dot(Vector3.down, slideAverageNormal) <= 0f)
				{
					base.transform.position = base.transform.position - Vector3.Project(Mathf.Min(stickDepth * scale, Vector3.Project(denormalizedVelocityAverage, slideAverageNormal).magnitude * calcDeltaTime) * slideAverageNormal, Vector3.down);
				}
			}
			if (!didAJump && (wasLeftHandSlide || wasRightHandSlide))
			{
				base.transform.position = base.transform.position + slideAverage * calcDeltaTime;
				slideAverage += 9.8f * Vector3.down * calcDeltaTime;
			}
			FirstHandIteration(leftHandTransform, leftHandOffset, LastLeftHandPosition(), wasLeftHandSlide, wasLeftHandTouching, out firstIterationLeftHand, ref leftHandSlipPercentage, ref leftHandSlide, ref leftHandSlideNormal, ref leftHandColliding, ref leftHandMaterialTouchIndex, ref leftHandSurfaceOverride);
			leftHandColliding = leftHandColliding && controllerState.LeftValid;
			leftHandSlide = leftHandSlide && controllerState.LeftValid;
			FirstHandIteration(rightHandTransform, rightHandOffset, LastRightHandPosition(), wasRightHandSlide, wasRightHandTouching, out firstIterationRightHand, ref rightHandSlipPercentage, ref rightHandSlide, ref rightHandSlideNormal, ref rightHandColliding, ref rightHandMaterialTouchIndex, ref rightHandSurfaceOverride);
			rightHandColliding = rightHandColliding && controllerState.RightValid;
			rightHandSlide = rightHandSlide && controllerState.RightValid;
			touchPoints = 0;
			rigidBodyMovement = Vector3.zero;
			if (leftHandColliding || wasLeftHandTouching)
			{
				rigidBodyMovement += firstIterationLeftHand;
				touchPoints++;
			}
			if (rightHandColliding || wasRightHandTouching)
			{
				rigidBodyMovement += firstIterationRightHand;
				touchPoints++;
			}
			if (touchPoints != 0)
			{
				rigidBodyMovement /= (float)touchPoints;
			}
			if (!MaxSphereSizeForNoOverlap(headCollider.radius * 0.9f * scale, lastHeadPosition, ignoreOneWay: true, out maxSphereSize1) && !CrazyCheck2(headCollider.radius * 0.9f * 0.75f * scale, lastHeadPosition))
			{
				lastHeadPosition = lastOpenHeadPosition;
			}
			if (IterativeCollisionSphereCast(lastHeadPosition, headCollider.radius * 0.9f * scale, headCollider.transform.position + rigidBodyMovement - lastHeadPosition, out finalPosition, singleHand: false, out slipPercentage, out junkHit, fullSlide: true))
			{
				rigidBodyMovement = finalPosition - headCollider.transform.position;
			}
			if (!MaxSphereSizeForNoOverlap(headCollider.radius * 0.9f * scale, lastHeadPosition + rigidBodyMovement, ignoreOneWay: true, out maxSphereSize1) || !CrazyCheck2(headCollider.radius * 0.9f * 0.75f * scale, lastHeadPosition + rigidBodyMovement))
			{
				lastHeadPosition = lastOpenHeadPosition;
				rigidBodyMovement = lastHeadPosition - headCollider.transform.position;
			}
			else if (headCollider.radius * 0.9f * 0.825f * scale < maxSphereSize1)
			{
				lastOpenHeadPosition = headCollider.transform.position + rigidBodyMovement;
			}
			if (rigidBodyMovement != Vector3.zero)
			{
				base.transform.position += rigidBodyMovement;
			}
			lastHeadPosition = headCollider.transform.position;
			areBothTouching = (!leftHandColliding && !wasLeftHandTouching) || (!rightHandColliding && !wasRightHandTouching);
			lastLeftHandPosition = FinalHandPosition(leftHandTransform, leftHandOffset, LastLeftHandPosition(), areBothTouching, leftHandColliding, out leftHandColliding, leftHandSlide, out leftHandSlide, leftHandMaterialTouchIndex, out leftHandMaterialTouchIndex, leftHandSurfaceOverride, out leftHandSurfaceOverride);
			leftHandColliding = leftHandColliding && controllerState.LeftValid;
			leftHandSlide = leftHandSlide && controllerState.LeftValid;
			lastRightHandPosition = FinalHandPosition(rightHandTransform, rightHandOffset, LastRightHandPosition(), areBothTouching, rightHandColliding, out rightHandColliding, rightHandSlide, out rightHandSlide, rightHandMaterialTouchIndex, out rightHandMaterialTouchIndex, rightHandSurfaceOverride, out rightHandSurfaceOverride);
			rightHandColliding = rightHandColliding && controllerState.RightValid;
			rightHandSlide = rightHandSlide && controllerState.RightValid;
			StoreVelocities();
			didAJump = false;
			if (OverrideSlipToMax())
			{
				didAJump = true;
			}
			else if (rightHandSlide || leftHandSlide)
			{
				slideAverageNormal = Vector3.zero;
				touchPoints = 0;
				averageSlipPercentage = 0f;
				if (leftHandSlide)
				{
					slideAverageNormal += leftHandSlideNormal.normalized;
					averageSlipPercentage += leftHandSlipPercentage;
					touchPoints++;
				}
				if (rightHandSlide)
				{
					slideAverageNormal += rightHandSlideNormal.normalized;
					averageSlipPercentage += rightHandSlipPercentage;
					touchPoints++;
				}
				slideAverageNormal = slideAverageNormal.normalized;
				averageSlipPercentage /= touchPoints;
				if (touchPoints == 1)
				{
					surfaceDirection = (rightHandSlide ? Vector3.ProjectOnPlane(rightHandTransform.forward, rightHandSlideNormal) : Vector3.ProjectOnPlane(leftHandTransform.forward, leftHandSlideNormal));
					if (Vector3.Dot(slideAverage, surfaceDirection) > 0f)
					{
						slideAverage = Vector3.Project(slideAverage, Vector3.Slerp(slideAverage, surfaceDirection.normalized * slideAverage.magnitude, slideControl));
					}
					else
					{
						slideAverage = Vector3.Project(slideAverage, Vector3.Slerp(slideAverage, -surfaceDirection.normalized * slideAverage.magnitude, slideControl));
					}
				}
				if (!wasLeftHandSlide && !wasRightHandSlide)
				{
					slideAverage = ((Vector3.Dot(playerRigidBody.velocity, slideAverageNormal) <= 0f) ? Vector3.ProjectOnPlane(playerRigidBody.velocity, slideAverageNormal) : playerRigidBody.velocity);
				}
				else
				{
					slideAverage = ((Vector3.Dot(slideAverage, slideAverageNormal) <= 0f) ? Vector3.ProjectOnPlane(slideAverage, slideAverageNormal) : slideAverage);
				}
				slideAverage = slideAverage.normalized * Mathf.Min(slideAverage.magnitude, Mathf.Max(0.5f, denormalizedVelocityAverage.magnitude * 2f));
				playerRigidBody.velocity = Vector3.zero;
			}
			else if (leftHandColliding || rightHandColliding)
			{
				if (!didATurn)
				{
					playerRigidBody.velocity = Vector3.zero;
				}
				else
				{
					playerRigidBody.velocity = playerRigidBody.velocity.normalized * Mathf.Min(2f, playerRigidBody.velocity.magnitude);
				}
			}
			else if (wasLeftHandSlide || wasRightHandSlide)
			{
				playerRigidBody.velocity = ((Vector3.Dot(slideAverage, slideAverageNormal) <= 0f) ? Vector3.ProjectOnPlane(slideAverage, slideAverageNormal) : slideAverage);
			}
			if ((rightHandColliding || leftHandColliding) && !disableMovement && !didATurn && !didAJump)
			{
				if (rightHandSlide || leftHandSlide)
				{
					if (Vector3.Project(denormalizedVelocityAverage, slideAverageNormal).magnitude > slideVelocityLimit && Vector3.Dot(denormalizedVelocityAverage, slideAverageNormal) > 0f && Vector3.Project(denormalizedVelocityAverage, slideAverageNormal).magnitude > Vector3.Project(slideAverage, slideAverageNormal).magnitude)
					{
						leftHandSlide = false;
						rightHandSlide = false;
						didAJump = true;
						playerRigidBody.velocity = Mathf.Min(maxJumpSpeed * ExtraVelMaxMultiplier(), jumpMultiplier * ExtraVelMultiplier() * Vector3.Project(denormalizedVelocityAverage, slideAverageNormal).magnitude) * slideAverageNormal.normalized + Vector3.ProjectOnPlane(slideAverage, slideAverageNormal);
					}
				}
				else if (denormalizedVelocityAverage.magnitude > velocityLimit * scale)
				{
					didAJump = true;
					playerRigidBody.velocity = Mathf.Min(maxJumpSpeed * ExtraVelMaxMultiplier(), jumpMultiplier * ExtraVelMultiplier() * denormalizedVelocityAverage.magnitude) * denormalizedVelocityAverage.normalized;
				}
			}
			if (!controllerState.LeftValid || (leftHandColliding && (CurrentLeftHandPosition() - LastLeftHandPosition()).magnitude > unStickDistance * scale && !Physics.Raycast(headCollider.transform.position, (CurrentLeftHandPosition() - headCollider.transform.position).normalized, out hitInfo, (CurrentLeftHandPosition() - headCollider.transform.position).magnitude, locomotionEnabledLayers.value)))
			{
				lastLeftHandPosition = CurrentLeftHandPosition();
				leftHandColliding = false;
			}
			if (!controllerState.RightValid || (rightHandColliding && (CurrentRightHandPosition() - LastRightHandPosition()).magnitude > unStickDistance * scale && !Physics.Raycast(headCollider.transform.position, (CurrentRightHandPosition() - headCollider.transform.position).normalized, out hitInfo, (CurrentRightHandPosition() - headCollider.transform.position).magnitude, locomotionEnabledLayers.value)))
			{
				lastRightHandPosition = CurrentRightHandPosition();
				rightHandColliding = false;
			}
			if (currentPlatform == null)
			{
				playerRigidBody.velocity += refMovement / calcDeltaTime;
				refMovement = Vector3.zero;
			}
			leftHandFollower.position = lastLeftHandPosition;
			rightHandFollower.position = lastRightHandPosition;
			wasLeftHandTouching = leftHandColliding;
			wasRightHandTouching = rightHandColliding;
			wasLeftHandSlide = leftHandSlide;
			wasRightHandSlide = rightHandSlide;
			didATurn = false;
			lastPlatformTouched = currentPlatform;
			currentPlatform = null;
			lastFrameTouchPosLocal = ComputeLocalHitPoint(lastHitInfoHand);
			lastFrameTouchPosWorld = lastHitInfoHand.point;
			BodyCollider();
		}

		private void FirstHandIteration(Transform handTransform, Vector3 handOffset, Vector3 lastHandPosition, bool wasHandSlide, bool wasHandTouching, out Vector3 firstIteration, ref float handSlipPercentage, ref bool handSlide, ref Vector3 slideNormal, ref bool handColliding, ref int materialTouchIndex, ref GorillaSurfaceOverride touchedOverride)
		{
			firstIteration = Vector3.zero;
			distanceTraveled = CurrentHandPosition(handTransform, handOffset) - lastHandPosition;
			if (!didAJump && wasHandSlide && Vector3.Dot(slideNormal, Vector3.up) > 0f)
			{
				distanceTraveled += Vector3.Project(-slideAverageNormal * stickDepth * scale, Vector3.down);
			}
			if (IterativeCollisionSphereCast(lastHandPosition, minimumRaycastDistance * scale, distanceTraveled, out finalPosition, singleHand: true, out slipPercentage, out tempHitInfo, fullSlide: false))
			{
				if (wasHandTouching && slipPercentage <= defaultSlideFactor)
				{
					firstIteration = lastHandPosition - CurrentHandPosition(handTransform, handOffset);
				}
				else
				{
					firstIteration = finalPosition - CurrentHandPosition(handTransform, handOffset);
				}
				handSlipPercentage = slipPercentage;
				handSlide = slipPercentage > iceThreshold;
				slideNormal = tempHitInfo.normal;
				handColliding = true;
				materialTouchIndex = currentMaterialIndex;
				touchedOverride = currentOverride;
				lastHitInfoHand = tempHitInfo;
			}
			else
			{
				handSlipPercentage = 0f;
				handSlide = false;
				slideNormal = Vector3.up;
				handColliding = false;
				materialTouchIndex = 0;
				touchedOverride = null;
			}
		}

		private Vector3 FinalHandPosition(Transform handTransform, Vector3 handOffset, Vector3 lastHandPosition, bool bothTouching, bool isHandTouching, out bool handColliding, bool isHandSlide, out bool handSlide, int currentMaterialTouchIndex, out int materialTouchIndex, GorillaSurfaceOverride currentSurface, out GorillaSurfaceOverride touchedOverride)
		{
			handColliding = isHandTouching;
			handSlide = isHandSlide;
			materialTouchIndex = currentMaterialTouchIndex;
			touchedOverride = currentSurface;
			distanceTraveled = CurrentHandPosition(handTransform, handOffset) - lastHandPosition;
			if (IterativeCollisionSphereCast(lastHandPosition, minimumRaycastDistance * scale, distanceTraveled, out finalPosition, bothTouching, out slipPercentage, out junkHit, fullSlide: false))
			{
				handColliding = true;
				handSlide = slipPercentage > iceThreshold;
				materialTouchIndex = currentMaterialIndex;
				touchedOverride = currentOverride;
				lastHitInfoHand = junkHit;
				return finalPosition;
			}
			return CurrentHandPosition(handTransform, handOffset);
		}

		private bool IterativeCollisionSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, out Vector3 endPosition, bool singleHand, out float slipPercentage, out RaycastHit iterativeHitInfo, bool fullSlide)
		{
			slipPercentage = defaultSlideFactor;
			if (CollisionsSphereCast(startPosition, sphereRadius, movementVector, out endPosition, out tempIterativeHit))
			{
				firstPosition = endPosition;
				iterativeHitInfo = tempIterativeHit;
				slideFactor = GetSlidePercentage(iterativeHitInfo);
				slipPercentage = ((slideFactor != defaultSlideFactor) ? slideFactor : ((!singleHand) ? defaultSlideFactor : 0.001f));
				if (fullSlide || OverrideSlipToMax())
				{
					slipPercentage = 1f;
				}
				movementToProjectedAboveCollisionPlane = Vector3.ProjectOnPlane(startPosition + movementVector - firstPosition, iterativeHitInfo.normal) * slipPercentage;
				if (CollisionsSphereCast(firstPosition, sphereRadius, movementToProjectedAboveCollisionPlane, out endPosition, out tempIterativeHit))
				{
					iterativeHitInfo = tempIterativeHit;
					return true;
				}
				if (CollisionsSphereCast(movementToProjectedAboveCollisionPlane + firstPosition, sphereRadius, startPosition + movementVector - (movementToProjectedAboveCollisionPlane + firstPosition), out endPosition, out tempIterativeHit))
				{
					iterativeHitInfo = tempIterativeHit;
					return true;
				}
				endPosition = Vector3.zero;
				return false;
			}
			iterativeHitInfo = tempIterativeHit;
			endPosition = Vector3.zero;
			return false;
		}

		private bool CollisionsSphereCast(Vector3 startPosition, float sphereRadius, Vector3 movementVector, out Vector3 finalPosition, out RaycastHit collisionsHitInfo)
		{
			MaxSphereSizeForNoOverlap(sphereRadius, startPosition, ignoreOneWay: false, out maxSphereSize1);
			ClearRaycasthitBuffer(ref rayCastNonAllocColliders);
			bufferCount = Physics.SphereCastNonAlloc(startPosition, maxSphereSize1, movementVector.normalized, rayCastNonAllocColliders, movementVector.magnitude, locomotionEnabledLayers.value);
			if (bufferCount > 0)
			{
				tempHitInfo = rayCastNonAllocColliders[0];
				for (int i = 0; i < bufferCount; i++)
				{
					if (rayCastNonAllocColliders[i].distance < tempHitInfo.distance)
					{
						tempHitInfo = rayCastNonAllocColliders[i];
					}
				}
				collisionsHitInfo = tempHitInfo;
				finalPosition = collisionsHitInfo.point + collisionsHitInfo.normal * sphereRadius;
				ClearRaycasthitBuffer(ref rayCastNonAllocColliders);
				bufferCount = Physics.RaycastNonAlloc(startPosition, (finalPosition - startPosition).normalized, rayCastNonAllocColliders, (finalPosition - startPosition).magnitude, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore);
				if (bufferCount > 0)
				{
					tempHitInfo = rayCastNonAllocColliders[0];
					for (int j = 0; j < bufferCount; j++)
					{
						if (rayCastNonAllocColliders[j].distance < tempHitInfo.distance)
						{
							tempHitInfo = rayCastNonAllocColliders[j];
						}
					}
					finalPosition = startPosition + movementVector.normalized * tempHitInfo.distance;
				}
				MaxSphereSizeForNoOverlap(sphereRadius, finalPosition, ignoreOneWay: false, out maxSphereSize2);
				ClearRaycasthitBuffer(ref rayCastNonAllocColliders);
				bufferCount = Physics.SphereCastNonAlloc(startPosition, Mathf.Min(maxSphereSize1, maxSphereSize2), (finalPosition - startPosition).normalized, rayCastNonAllocColliders, (finalPosition - startPosition).magnitude, locomotionEnabledLayers.value);
				if (bufferCount > 0)
				{
					tempHitInfo = rayCastNonAllocColliders[0];
					for (int k = 0; k < bufferCount; k++)
					{
						if (rayCastNonAllocColliders[k].collider != null && rayCastNonAllocColliders[k].distance < tempHitInfo.distance)
						{
							tempHitInfo = rayCastNonAllocColliders[k];
						}
					}
					finalPosition = startPosition + tempHitInfo.distance * (finalPosition - startPosition).normalized;
					collisionsHitInfo = tempHitInfo;
				}
				ClearRaycasthitBuffer(ref rayCastNonAllocColliders);
				bufferCount = Physics.RaycastNonAlloc(startPosition, (finalPosition - startPosition).normalized, rayCastNonAllocColliders, (finalPosition - startPosition).magnitude, locomotionEnabledLayers.value);
				if (bufferCount > 0)
				{
					tempHitInfo = rayCastNonAllocColliders[0];
					for (int l = 0; l < bufferCount; l++)
					{
						if (rayCastNonAllocColliders[l].distance < tempHitInfo.distance)
						{
							tempHitInfo = rayCastNonAllocColliders[l];
						}
					}
					collisionsHitInfo = tempHitInfo;
					finalPosition = startPosition;
				}
				return true;
			}
			ClearRaycasthitBuffer(ref rayCastNonAllocColliders);
			bufferCount = Physics.RaycastNonAlloc(startPosition, movementVector.normalized, rayCastNonAllocColliders, movementVector.magnitude, locomotionEnabledLayers.value);
			if (bufferCount > 0)
			{
				tempHitInfo = rayCastNonAllocColliders[0];
				for (int m = 0; m < bufferCount; m++)
				{
					if (rayCastNonAllocColliders[m].collider != null && rayCastNonAllocColliders[m].distance < tempHitInfo.distance)
					{
						tempHitInfo = rayCastNonAllocColliders[m];
					}
				}
				collisionsHitInfo = tempHitInfo;
				finalPosition = startPosition;
				return true;
			}
			finalPosition = startPosition + movementVector;
			collisionsHitInfo = default(RaycastHit);
			return false;
		}

		public bool IsHandTouching(bool forLeftHand)
		{
			if (forLeftHand)
			{
				return wasLeftHandTouching;
			}
			return wasRightHandTouching;
		}

		public bool IsHandSliding(bool forLeftHand)
		{
			if (forLeftHand)
			{
				if (!wasLeftHandSlide)
				{
					return leftHandSlide;
				}
				return true;
			}
			if (!wasRightHandSlide)
			{
				return rightHandSlide;
			}
			return true;
		}

		public float GetSlidePercentage(RaycastHit raycastHit)
		{
			currentOverride = raycastHit.collider.gameObject.GetComponent<GorillaSurfaceOverride>();
			Platform component = raycastHit.collider.gameObject.GetComponent<Platform>();
			if (component != null)
			{
				currentPlatform = component;
			}
			if (currentOverride != null)
			{
				currentMaterialIndex = currentOverride.overrideIndex;
				if (!materialData[currentMaterialIndex].overrideSlidePercent)
				{
					return defaultSlideFactor;
				}
				return materialData[currentMaterialIndex].slidePercent;
			}
			meshCollider = raycastHit.collider as MeshCollider;
			if (meshCollider == null || meshCollider.sharedMesh == null)
			{
				return defaultSlideFactor;
			}
			collidedMesh = meshCollider.sharedMesh;
			if (!meshTrianglesDict.TryGetValue(collidedMesh, out sharedMeshTris))
			{
				sharedMeshTris = collidedMesh.triangles;
				meshTrianglesDict.Add(collidedMesh, (int[])sharedMeshTris.Clone());
			}
			vertex1 = sharedMeshTris[raycastHit.triangleIndex * 3];
			vertex2 = sharedMeshTris[raycastHit.triangleIndex * 3 + 1];
			vertex3 = sharedMeshTris[raycastHit.triangleIndex * 3 + 2];
			slideRenderer = raycastHit.collider.GetComponent<Renderer>();
			slideRenderer.GetSharedMaterials(tempMaterialArray);
			if (tempMaterialArray.Count > 1)
			{
				for (int i = 0; i < tempMaterialArray.Count; i++)
				{
					collidedMesh.GetTriangles(trianglesList, i);
					for (int j = 0; j < trianglesList.Count; j += 3)
					{
						if (trianglesList[j] == vertex1 && trianglesList[j + 1] == vertex2 && trianglesList[j + 2] == vertex3)
						{
							findMatName = tempMaterialArray[i].name;
							foundMatData = materialData.Find((MaterialData matData) => matData.matName == findMatName);
							currentMaterialIndex = materialData.FindIndex((MaterialData matData) => matData.matName == findMatName);
							if (currentMaterialIndex == -1)
							{
								currentMaterialIndex = 0;
							}
							if (!foundMatData.overrideSlidePercent)
							{
								return defaultSlideFactor;
							}
							return foundMatData.slidePercent;
						}
					}
				}
				currentMaterialIndex = 0;
				return defaultSlideFactor;
			}
			findMatName = tempMaterialArray[0].name;
			foundMatData = materialData.Find((MaterialData matData) => matData.matName == findMatName);
			currentMaterialIndex = materialData.FindIndex((MaterialData matData) => matData.matName == findMatName);
			if (currentMaterialIndex == -1)
			{
				currentMaterialIndex = 0;
			}
			if (!foundMatData.overrideSlidePercent)
			{
				return defaultSlideFactor;
			}
			return foundMatData.slidePercent;
		}

		public void Turn(float degrees)
		{
			turnParent.transform.RotateAround(headCollider.transform.position, base.transform.up, degrees);
			denormalizedVelocityAverage = Vector3.zero;
			for (int i = 0; i < velocityHistory.Length; i++)
			{
				velocityHistory[i] = Quaternion.Euler(0f, degrees, 0f) * velocityHistory[i];
				denormalizedVelocityAverage += velocityHistory[i];
			}
			didATurn = true;
		}

		private void StoreVelocities()
		{
			velocityIndex = (velocityIndex + 1) % velocityHistorySize;
			currentVelocity = (base.transform.position - lastPosition - MovingSurfaceMovement()) / calcDeltaTime;
			velocityHistory[velocityIndex] = currentVelocity;
			denormalizedVelocityAverage = Vector3.zero;
			for (int i = 0; i < velocityHistory.Length; i++)
			{
				denormalizedVelocityAverage += velocityHistory[i];
			}
			denormalizedVelocityAverage /= (float)velocityHistorySize;
			lastPosition = base.transform.position;
		}

		private void AntiTeleportTechnology()
		{
			if ((headCollider.transform.position - lastHeadPosition).magnitude >= teleportThresholdNoVel + playerRigidBody.velocity.magnitude * calcDeltaTime)
			{
				base.transform.position = base.transform.position + lastHeadPosition - headCollider.transform.position;
			}
		}

		private bool MaxSphereSizeForNoOverlap(float testRadius, Vector3 checkPosition, bool ignoreOneWay, out float overlapRadiusTest)
		{
			overlapRadiusTest = testRadius;
			overlapAttempts = 0;
			while (overlapAttempts < 100 && overlapRadiusTest > testRadius * 0.75f)
			{
				ClearColliderBuffer(ref overlapColliders);
				bufferCount = Physics.OverlapSphereNonAlloc(checkPosition, overlapRadiusTest, overlapColliders, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore);
				if (ignoreOneWay)
				{
					int num = 0;
					for (int i = 0; i < bufferCount; i++)
					{
						if (overlapColliders[i].CompareTag("NoCrazyCheck"))
						{
							num++;
						}
					}
					if (num == bufferCount)
					{
						return true;
					}
				}
				if (bufferCount > 0)
				{
					overlapRadiusTest *= 0.99f;
					overlapAttempts++;
					continue;
				}
				overlapRadiusTest *= 0.995f;
				return true;
			}
			return false;
		}

		private bool CrazyCheck2(float sphereSize, Vector3 startPosition)
		{
			for (int i = 0; i < crazyCheckVectors.Length; i++)
			{
				if (NonAllocRaycast(startPosition, startPosition + crazyCheckVectors[i] * sphereSize) > 0)
				{
					return false;
				}
			}
			return true;
		}

		private int NonAllocRaycast(Vector3 startPosition, Vector3 endPosition)
		{
			Vector3 direction = endPosition - startPosition;
			int num = Physics.RaycastNonAlloc(startPosition, direction, rayCastNonAllocColliders, direction.magnitude, locomotionEnabledLayers.value, QueryTriggerInteraction.Ignore);
			int num2 = 0;
			for (int i = 0; i < num; i++)
			{
				if (!rayCastNonAllocColliders[i].collider.gameObject.CompareTag("NoCrazyCheck"))
				{
					num2++;
				}
			}
			return num2;
		}

		private void ClearColliderBuffer(ref Collider[] colliders)
		{
			for (int i = 0; i < colliders.Length; i++)
			{
				colliders[i] = null;
			}
		}

		private void ClearRaycasthitBuffer(ref RaycastHit[] raycastHits)
		{
			for (int i = 0; i < raycastHits.Length; i++)
			{
				raycastHits[i] = emptyHit;
			}
		}

		private Vector3 MovingSurfaceMovement()
		{
			return refMovement;
		}

		private static Vector3 ComputeLocalHitPoint(RaycastHit hit)
		{
			if (hit.collider == null)
			{
				return Vector3.zero;
			}
			return hit.collider.transform.InverseTransformPoint(hit.point);
		}

		private static Vector3 ComputeWorldHitPoint(RaycastHit hit, Vector3 localPoint)
		{
			if (hit.collider == null)
			{
				return Vector3.zero;
			}
			return hit.collider.transform.TransformPoint(localPoint);
		}

		private float ExtraVelMultiplier()
		{
			float num = 1f;
			if (leftHandSurfaceOverride != null)
			{
				num = Mathf.Max(num, leftHandSurfaceOverride.extraVelMultiplier);
			}
			if (rightHandSurfaceOverride != null)
			{
				num = Mathf.Max(num, rightHandSurfaceOverride.extraVelMultiplier);
			}
			return num;
		}

		private float ExtraVelMaxMultiplier()
		{
			float num = 1f;
			if (leftHandSurfaceOverride != null)
			{
				num = Mathf.Max(num, leftHandSurfaceOverride.extraVelMaxMultiplier);
			}
			if (rightHandSurfaceOverride != null)
			{
				num = Mathf.Max(num, rightHandSurfaceOverride.extraVelMaxMultiplier);
			}
			return num * scale;
		}

		public void SetMaximumSlipThisFrame()
		{
			disableGripFrameIdx = Time.frameCount;
		}

		public bool OverrideSlipToMax()
		{
			return disableGripFrameIdx == Time.frameCount;
		}
	}
}
