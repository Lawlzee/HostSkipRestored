using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using System.IO;
using RoR2;
using UnityEngine.AddressableAssets;
using System.Linq;
using KinematicCharacterController;
using MonoMod.RuntimeDetour;
using System.Reflection;
using System.Runtime;
using System;

//[assembly: HG.Reflection.SearchableAttribute.OptIn]

namespace HostSkipRestored;

[BepInPlugin(PluginGUID, PluginName, PluginVersion)]
public class HostSkipRestoredPlugin : BaseUnityPlugin
{
    public const string PluginGUID = "Lawlzee.HostSkipRestored";
    public const string PluginAuthor = "Lawlzee";
    public const string PluginName = "HostSkipRestored";
    public const string PluginVersion = "1.0.0";

    public void Awake()
    {
        Log.Init(Logger);

        On.RoR2.TeleportHelper.OnTeleport += TeleportHelper_OnTeleport;

        Log.Debug(typeof(KinematicCharacterSystem).GetMethod(nameof(KinematicCharacterSystem.FixedUpdate), BindingFlags.Instance | BindingFlags.NonPublic));

        new Hook(
            typeof(KinematicCharacterSystem).GetMethod(nameof(KinematicCharacterSystem.FixedUpdate), BindingFlags.Instance | BindingFlags.NonPublic),
            KinematicCharacterSystem_FixedUpdate);

        Log.Debug(typeof(KinematicCharacterMotor).GetMethod(nameof(KinematicCharacterMotor.UpdatePhase1)));
        new Hook(
            typeof(KinematicCharacterMotor).GetMethod(nameof(KinematicCharacterMotor.UpdatePhase1)),
            KinematicCharacterMotor_UpdatePhase1);

        Log.Debug(typeof(KinematicCharacterMotor).GetMethod(nameof(KinematicCharacterMotor.UpdatePhase2)));
        new Hook(
            typeof(KinematicCharacterMotor).GetMethod(nameof(KinematicCharacterMotor.UpdatePhase2)),
            KinematicCharacterMotor_UpdatePhase2);
        
        Log.Debug(typeof(KinematicCharacterMotor).GetMethod(nameof(KinematicCharacterMotor.InternalCharacterMove), BindingFlags.Instance | BindingFlags.NonPublic));
        new Hook(
            typeof(KinematicCharacterMotor).GetMethod(nameof(KinematicCharacterMotor.InternalCharacterMove), BindingFlags.Instance | BindingFlags.NonPublic),
            KinematicCharacterMotor_InternalCharacterMove);
        
        //Log.Debug(typeof(KinematicCharacterMotor).GetMethod(nameof(KinematicCharacterMotor.InternalHandleMovementProjection), BindingFlags.Instance | BindingFlags.NonPublic));
        //new Hook(
        //    typeof(KinematicCharacterMotor).GetMethod(nameof(KinematicCharacterMotor.InternalHandleMovementProjection), BindingFlags.Instance | BindingFlags.NonPublic),
        //    KinematicCharacterMotor_InternalHandleMovementProjection);
        //
        //Log.Debug(typeof(BaseCharacterController).GetMethod(nameof(BaseCharacterController.HandleMovementProjection)));
        //new Hook(
        //    typeof(BaseCharacterController).GetMethod(nameof(BaseCharacterController.HandleMovementProjection)),
        //    BaseCharacterController_HandleMovementProjection);
    }

    private bool _tpContext;

    private void TeleportHelper_OnTeleport(On.RoR2.TeleportHelper.orig_OnTeleport orig, GameObject gameObject, Vector3 newPosition, Vector3 delta)
    {
        _tpContext = true;

        Log.Debug(nameof(TeleportHelper_OnTeleport));
        Log.Debug(new
        {
            name = gameObject.name,
            newPosition,
            delta
        });
        orig(gameObject, newPosition, delta);
    }

    public void KinematicCharacterSystem_FixedUpdate(Action<KinematicCharacterSystem> orig, KinematicCharacterSystem self)
    {
        orig(self);
        if (_tpContext)
        {
            Log.Debug(nameof(KinematicCharacterSystem_FixedUpdate));
        }
        _tpContext = false;
    }

    public void KinematicCharacterMotor_UpdatePhase1(Action<KinematicCharacterMotor, float, bool> orig, KinematicCharacterMotor self, float deltaTime, bool doExpensive)
    {
        if (!_tpContext || self.gameObject.name != "HuntressBody(Clone)")
        {
            orig(self, deltaTime, doExpensive);
            return;
        }

        Log.Debug("KinematicCharacterMotor_UpdatePhase1");

        if (float.IsNaN(self.BaseVelocity.x) || float.IsNaN(self.BaseVelocity.y) || float.IsNaN(self.BaseVelocity.z))
        {
            Log.Debug("float.IsNaN(self.BaseVelocity.x) || float.IsNaN(self.BaseVelocity.y) || float.IsNaN(self.BaseVelocity.z)");
            self.BaseVelocity = Vector3.zero;
        }

        Log.Debug("self.BaseVelocity: " + self.BaseVelocity);

        if (float.IsNaN(self._attachedRigidbodyVelocity.x) || float.IsNaN(self._attachedRigidbodyVelocity.y) || float.IsNaN(self._attachedRigidbodyVelocity.z))
        {
            Log.Debug("float.IsNaN(self._attachedRigidbodyVelocity.x) || float.IsNaN(self._attachedRigidbodyVelocity.y) || float.IsNaN(self._attachedRigidbodyVelocity.z)");
            self._attachedRigidbodyVelocity = Vector3.zero;
        }

        Log.Debug("self._attachedRigidbodyVelocity : " + self._attachedRigidbodyVelocity);

        self.timeUntilUpdate -= deltaTime;
        if (self.playerCharacter | doExpensive || self._mustUnground || !self.GroundingStatus.IsStableOnGround)
        {
            Log.Debug("self.playerCharacter | doExpensive || self._mustUnground || !self.GroundingStatus.IsStableOnGround");
            self.timeUntilUpdate = KinematicCharacterMotor.updateTimerMax;
            self.doingUpdate = true;
            self._rigidbodiesPushedThisMove.Clear();
            self.CharacterController.BeforeCharacterUpdate(deltaTime);
            self._transientPosition = self._transform.position;
            self.TransientRotation = self._transform.rotation;
            self._initialSimulationPosition = self._transientPosition;
            self._initialSimulationRotation = self._transientRotation;
            self._rigidbodyProjectionHitCount = 0;
            self._overlapsCount = 0;
            self._lastSolvedOverlapNormalDirty = false;
            if (self._movePositionDirty)
            {
                Log.Debug("self._movePositionDirty");
                if (self._solveMovementCollisions)
                {
                    Log.Debug("self._solveMovementCollisions");
                    Vector3 velocityFromMovement = self.GetVelocityFromMovement(self._movePositionTarget - self._transientPosition, deltaTime);
                    Log.Debug("velocityFromMovement: " + velocityFromMovement);
                    if (self.InternalCharacterMove(ref velocityFromMovement, deltaTime) && self.InteractiveRigidbodyHandling)
                    {
                        Log.Debug("velocityFromMovement: " + velocityFromMovement);
                        Log.Debug("self.InternalCharacterMove(ref velocityFromMovement, deltaTime) && self.InteractiveRigidbodyHandling");
                        self.ProcessVelocityForRigidbodyHits(ref velocityFromMovement, deltaTime);
                    }
                    Log.Debug("velocityFromMovement: " + velocityFromMovement);
                }
                else
                {
                    Log.Debug("self._movePositionDirty");
                    self._transientPosition = self._movePositionTarget;
                }

                self._movePositionDirty = false;
            }
            self.LastGroundingStatus.CopyFrom(self.GroundingStatus);
            self.GroundingStatus = new CharacterGroundingReport();
            self.GroundingStatus.GroundNormal = self._characterUp;
            if (self._solveMovementCollisions)
            {
                Log.Debug("self._solveMovementCollisions");
                Vector3 direction = self._cachedWorldUp;
                float distance = 0.0f;
                int num1 = 0;
                for (bool flag = false; num1 < 3 && !flag; ++num1)
                {
                    int num2 = self.CharacterCollisionsOverlap(self._transientPosition, self._transientRotation, self._internalProbedColliders);
                    if (num2 > 0)
                    {
                        Log.Debug("num2 > 0");
                        for (int index = 0; index < num2; ++index)
                        {
                            if ((UnityEngine.Object)self.GetInteractiveRigidbody(self._internalProbedColliders[index]) == (UnityEngine.Object)null)
                            {
                                Log.Debug("(UnityEngine.Object)self.GetInteractiveRigidbody(self._internalProbedColliders[index]) == (UnityEngine.Object)null");
                                Transform component = self._internalProbedColliders[index].GetComponent<Transform>();
                                if (Physics.ComputePenetration((Collider)self.Capsule, self._transientPosition, self._transientRotation, self._internalProbedColliders[index], component.position, component.rotation, out direction, out distance))
                                {
                                    Log.Debug("Physics.ComputePenetration((Collider)self.Capsule, self._transientPosition, self._transientRotation, self._internalProbedColliders[index], component.position, component.rotation, out direction, out distance)");
                                    direction = self.GetObstructionNormal(direction, new HitStabilityReport()
                                    {
                                        IsStable = self.IsStableOnNormal(direction)
                                    }.IsStable);
                                    self._transientPosition += direction * (distance + 0.01f);
                                    if (self._overlapsCount < self._overlaps.Length)
                                    {
                                        Log.Debug("self._overlapsCount < self._overlaps.Length");
                                        self._overlaps[self._overlapsCount] = new OverlapResult(direction, self._internalProbedColliders[index]);
                                        ++self._overlapsCount;
                                        break;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        Log.Debug("!num2 > 0");
                        flag = true;
                    }
                }
            }
            if (self._solveGrounding)
            {
                Log.Debug("self._solveGrounding");
                if (self.MustUnground())
                {
                    Log.Debug("self.MustUnground()");
                    self._transientPosition += self._characterUp * 0.0075f;
                }
                else
                {
                    Log.Debug("!self.MustUnground()");
                    float probingDistance = 0.005f;
                    if (!self.LastGroundingStatus.SnappingPrevented && (self.LastGroundingStatus.IsStableOnGround || self.LastMovementIterationFoundAnyGround))
                    {
                        Log.Debug("!self.LastGroundingStatus.SnappingPrevented && (self.LastGroundingStatus.IsStableOnGround || self.LastMovementIterationFoundAnyGround)");
                        probingDistance = (self.StepHandling == StepHandlingMethod.None ? self.CapsuleRadius : Mathf.Max(self.CapsuleRadius, self.MaxStepHeight)) + self.GroundDetectionExtraDistance;
                    }

                    self.ProbeGround(ref self._transientPosition, self._transientRotation, probingDistance, ref self.GroundingStatus);
                    if (!self.LastGroundingStatus.IsStableOnGround && self.GroundingStatus.IsStableOnGround)
                    {
                        Log.Debug("!self.LastGroundingStatus.IsStableOnGround && self.GroundingStatus.IsStableOnGround");
                        Log.Debug("self.BaseVelocity: " + self.BaseVelocity);
                        self.BaseVelocity = Vector3.ProjectOnPlane(self.BaseVelocity, self.CharacterUp);
                        Log.Debug("self.BaseVelocity: " + self.BaseVelocity);
                        self.BaseVelocity = self.GetDirectionTangentToSurface(self.BaseVelocity, self.GroundingStatus.GroundNormal) * self.BaseVelocity.magnitude;
                        Log.Debug("self.BaseVelocity: " + self.BaseVelocity);
                    }
                }
            }
            self.LastMovementIterationFoundAnyGround = false;
            if ((double)self._mustUngroundTimeCounter > 0.0)
            {
                Log.Debug("(double)self._mustUngroundTimeCounter > 0.0");
                self._mustUngroundTimeCounter -= deltaTime;
            }

            self._mustUnground = false;
            if (self._solveGrounding)
            {
                Log.Debug("self._solveGrounding");
                self.CharacterController.PostGroundingUpdate(deltaTime);
            }

            if (!self.InteractiveRigidbodyHandling)
            {
                Log.Debug("!self.InteractiveRigidbodyHandling");
                return;
            }

            self._lastAttachedRigidbody = self._attachedRigidbody;
            if ((bool)(UnityEngine.Object)self.AttachedRigidbodyOverride)
            {
                Log.Debug("(bool)(UnityEngine.Object)self.AttachedRigidbodyOverride");
                self._attachedRigidbody = self.AttachedRigidbodyOverride;
            }
            else if (self.GroundingStatus.IsStableOnGround && (bool)(UnityEngine.Object)self.GroundingStatus.GroundCollider.attachedRigidbody)
            {
                Log.Debug("self.GroundingStatus.IsStableOnGround && (bool)(UnityEngine.Object)self.GroundingStatus.GroundCollider.attachedRigidbody");
                Rigidbody interactiveRigidbody = self.GetInteractiveRigidbody(self.GroundingStatus.GroundCollider);
                if ((bool)(UnityEngine.Object)interactiveRigidbody)
                {
                    Log.Debug("(bool)(UnityEngine.Object)interactiveRigidbody");
                    self._attachedRigidbody = interactiveRigidbody;
                }
            }
            else
            {
                Log.Debug("else");
                self._attachedRigidbody = (Rigidbody)null;
            }

            Vector3 linearVelocity = Vector3.zero;
            Vector3 angularVelocity = Vector3.zero;
            if ((bool)(UnityEngine.Object)self._attachedRigidbody)
            {
                Log.Debug("(bool)(UnityEngine.Object)self._attachedRigidbody");
                self.GetVelocityFromRigidbodyMovement(self._attachedRigidbody, self._transientPosition, deltaTime, out linearVelocity, out angularVelocity);
                Log.Debug("linearVelocity: " + linearVelocity);
                Log.Debug("angularVelocity: " + angularVelocity);
            }

            if (self.PreserveAttachedRigidbodyMomentum && (UnityEngine.Object)self._lastAttachedRigidbody != (UnityEngine.Object)null && (UnityEngine.Object)self._attachedRigidbody != (UnityEngine.Object)self._lastAttachedRigidbody)
            {
                Log.Debug("self.PreserveAttachedRigidbodyMomentum && (UnityEngine.Object)self._lastAttachedRigidbody != (UnityEngine.Object)null && (UnityEngine.Object)self._attachedRigidbody != (UnityEngine.Object)self._lastAttachedRigidbody");
                Log.Debug("self.BaseVelocity: " + self.BaseVelocity);
                self.BaseVelocity += self._attachedRigidbodyVelocity;
                Log.Debug("self.BaseVelocity: " + self.BaseVelocity);
                self.BaseVelocity -= linearVelocity;
                Log.Debug("self.BaseVelocity: " + self.BaseVelocity);
            }
            self._attachedRigidbodyVelocity = self._cachedZeroVector;
            if ((bool)(UnityEngine.Object)self._attachedRigidbody)
            {
                Log.Debug("(bool)(UnityEngine.Object)self._attachedRigidbody");
                self._attachedRigidbodyVelocity = linearVelocity;
                self.TransientRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Quaternion.Euler(57.29578f * angularVelocity * deltaTime) * self._characterForward, self._characterUp).normalized, self._characterUp);
            }
            if ((bool)(UnityEngine.Object)self.GroundingStatus.GroundCollider && (bool)(UnityEngine.Object)self.GroundingStatus.GroundCollider.attachedRigidbody && (UnityEngine.Object)self.GroundingStatus.GroundCollider.attachedRigidbody == (UnityEngine.Object)self._attachedRigidbody && (UnityEngine.Object)self._attachedRigidbody != (UnityEngine.Object)null && (UnityEngine.Object)self._lastAttachedRigidbody == (UnityEngine.Object)null)
            {
                Log.Debug("(bool)(UnityEngine.Object)self.GroundingStatus.GroundCollider && (bool)(UnityEngine.Object)self.GroundingStatus.GroundCollider.attachedRigidbody && (UnityEngine.Object)self.GroundingStatus.GroundCollider.attachedRigidbody == (UnityEngine.Object)self._attachedRigidbody && (UnityEngine.Object)self._attachedRigidbody != (UnityEngine.Object)null && (UnityEngine.Object)self._lastAttachedRigidbody == (UnityEngine.Object)null");
                Log.Debug("self.BaseVelocity: " + self.BaseVelocity);
                self.BaseVelocity -= Vector3.ProjectOnPlane(self._attachedRigidbodyVelocity, self._characterUp);
                Log.Debug("self.BaseVelocity: " + self.BaseVelocity);
            }

            if ((double)self._attachedRigidbodyVelocity.sqrMagnitude <= 0.0)
            {
                Log.Debug("(double)self._attachedRigidbodyVelocity.sqrMagnitude <= 0.0");
                return;
            }

            self._isMovingFromAttachedRigidbody = true;
            if (self._solveMovementCollisions)
            {
                Log.Debug("self._solveMovementCollisions");
                self.InternalCharacterMove(ref self._attachedRigidbodyVelocity, deltaTime);
                Log.Debug("self._attachedRigidbodyVelocity: " + self._attachedRigidbodyVelocity);
            }
            else
            {
                Log.Debug("!self._solveMovementCollisions");
                self._transientPosition += self._attachedRigidbodyVelocity * deltaTime;
            }

            self._isMovingFromAttachedRigidbody = false;
        }
        else
        {
            Log.Debug("self.playerCharacter | doExpensive || self._mustUnground || !self.GroundingStatus.IsStableOnGround");
            self.CharacterController.BeforeCharacterUpdate(deltaTime);
            if (self._movePositionDirty)
            {
                Log.Debug("self._movePositionDirty");
                Vector3 rhs = self._movePositionTarget - self._transientPosition;
                float num = Vector3.Dot(self.GroundingStatus.GroundNormal, rhs);
                self._transientPosition += rhs - num * self.GroundingStatus.GroundNormal;
                self._moveRotationDirty = false;
            }
            self.LastGroundingStatus.CopyFrom(self.GroundingStatus);
        }
    }

    public void KinematicCharacterMotor_UpdatePhase2(Action<KinematicCharacterMotor, float, bool> orig, KinematicCharacterMotor self, float deltaTime, bool doExpensive = true)
    {
        if (!_tpContext || self.gameObject.name != "HuntressBody(Clone)")
        {
            orig(self, deltaTime, doExpensive);
            return;
        }

        Log.Debug("KinematicCharacterMotor_UpdatePhase2");

        if (doExpensive)
        {
            Log.Debug("doExpensive");
            self.CharacterController.UpdateRotation(ref self._transientRotation, deltaTime);
            self.TransientRotation = self._transientRotation;
            if (self._moveRotationDirty)
            {
                Log.Debug("self._moveRotationDirty");
                self.TransientRotation = self._moveRotationTarget;
                self._moveRotationDirty = false;
            }
            if (self._solveMovementCollisions && self.InteractiveRigidbodyHandling)
            {
                Log.Debug("self._solveMovementCollisions && self.InteractiveRigidbodyHandling");
                if (self.InteractiveRigidbodyHandling && (bool)(UnityEngine.Object)self._attachedRigidbody)
                {
                    Log.Debug("self.InteractiveRigidbodyHandling && (bool)(UnityEngine.Object)self._attachedRigidbody");
                    float radius = self.Capsule.radius;
                    RaycastHit closestHit;
                    if (self.CharacterGroundSweep(self._transientPosition + self._characterUp * radius, self._transientRotation, -self._characterUp, radius, out closestHit) && (UnityEngine.Object)closestHit.collider.attachedRigidbody == (UnityEngine.Object)self._attachedRigidbody && self.IsStableOnNormal(closestHit.normal))
                    {
                        Log.Debug("self.CharacterGroundSweep(self._transientPosition + self._characterUp * radius, self._transientRotation, -self._characterUp, radius, out closestHit) && (UnityEngine.Object)closestHit.collider.attachedRigidbody == (UnityEngine.Object)self._attachedRigidbody && self.IsStableOnNormal(closestHit.normal)");
                        self._transientPosition = self._transientPosition + self._characterUp * (radius - closestHit.distance) + self._characterUp * 0.01f;
                    }
                }
                if (self.InteractiveRigidbodyHandling)
                {
                    Log.Debug("self.InteractiveRigidbodyHandling");

                    Vector3 direction = self._cachedWorldUp;
                    float distance = 0.0f;
                    int num1 = 0;
                    for (bool flag = false; num1 < 3 && !flag; ++num1)
                    {
                        int num2 = self.CharacterCollisionsOverlap(self._transientPosition, self._transientRotation, self._internalProbedColliders);
                        if (num2 > 0)
                        {
                            Log.Debug("num2 > 0");
                            for (int index = 0; index < num2; ++index)
                            {
                                Transform component = self._internalProbedColliders[index].GetComponent<Transform>();
                                if (Physics.ComputePenetration((Collider)self.Capsule, self._transientPosition, self._transientRotation, self._internalProbedColliders[index], component.position, component.rotation, out direction, out distance))
                                {
                                    Log.Debug("Physics.ComputePenetration((Collider)self.Capsule, self._transientPosition, self._transientRotation, self._internalProbedColliders[index], component.position, component.rotation, out direction, out distance)");
                                    direction = self.GetObstructionNormal(direction, new HitStabilityReport()
                                    {
                                        IsStable = self.IsStableOnNormal(direction)
                                    }.IsStable);
                                    self._transientPosition += direction * (distance + 0.01f);
                                    if (self.InteractiveRigidbodyHandling)
                                    {
                                        Log.Debug("self.InteractiveRigidbodyHandling");
                                        Rigidbody interactiveRigidbody = self.GetInteractiveRigidbody(self._internalProbedColliders[index]);
                                        if ((UnityEngine.Object)interactiveRigidbody != (UnityEngine.Object)null)
                                        {
                                            Log.Debug("(UnityEngine.Object)interactiveRigidbody != (UnityEngine.Object)null");
                                            HitStabilityReport hitStabilityReport = new HitStabilityReport();
                                            hitStabilityReport.IsStable = self.IsStableOnNormal(direction);
                                            if (hitStabilityReport.IsStable)
                                            {
                                                Log.Debug("hitStabilityReport.IsStable");
                                                self.LastMovementIterationFoundAnyGround = hitStabilityReport.IsStable;
                                            }

                                            if ((UnityEngine.Object)interactiveRigidbody != (UnityEngine.Object)self._attachedRigidbody)
                                            {
                                                Log.Debug("(UnityEngine.Object)interactiveRigidbody != (UnityEngine.Object)self._attachedRigidbody");
                                                Vector3 vector3 = self._transientPosition + self._transientRotation * self._characterTransformToCapsuleCenter;
                                                Vector3 transientPosition = self._transientPosition;
                                                self.StoreRigidbodyHit(interactiveRigidbody, self.Velocity, transientPosition, direction, hitStabilityReport);
                                            }
                                        }
                                    }
                                    if (self._overlapsCount < self._overlaps.Length)
                                    {
                                        Log.Debug("self._overlapsCount < self._overlaps.Length");
                                        self._overlaps[self._overlapsCount] = new OverlapResult(direction, self._internalProbedColliders[index]);
                                        ++self._overlapsCount;
                                        break;
                                    }
                                    break;
                                }
                            }
                        }
                        else
                        {
                            Log.Debug("!num2 > 0");
                            flag = true;
                        }
                    }
                }
            }
            self.CharacterController.UpdateVelocity(ref self.BaseVelocity, deltaTime);
            if ((double)self.BaseVelocity.magnitude < 0.00999999977648258)
            {
                Log.Debug("(double)self.BaseVelocity.magnitude < 0.00999999977648258");
                self.BaseVelocity = Vector3.zero;
            }

            Log.Debug("self.BaseVelocity: " + self.BaseVelocity);

            if ((double)self.BaseVelocity.sqrMagnitude > 0.0)
            {
                Log.Debug("(double)self.BaseVelocity.sqrMagnitude > 0.0");
                if (self._solveMovementCollisions)
                {
                    Log.Debug("self._solveMovementCollisions");
                    Log.Debug("self.BaseVelocity.magnitude: " + self.BaseVelocity.magnitude);
                    Log.Debug("self.BaseVelocity.magnitude * deltaTime: " + self.BaseVelocity.magnitude * deltaTime);
                    var a = self.BaseVelocity.magnitude * deltaTime;
                    float magnitude = self.BaseVelocity.magnitude;
                    self.InternalCharacterMove(ref self.BaseVelocity, deltaTime);
                    Log.Debug("a * self.BaseVelocity: " + a * self.BaseVelocity);
                    Log.Debug("a * self.BaseVelocity / deltaTime: " + a * self.BaseVelocity / deltaTime);
                    Log.Debug("deltaTime: " + deltaTime);
                    Log.Debug("remainingMovementMagnitude: " + remainingMovementMagnitude);
                    Log.Debug("self.BaseVelocity: " + self.BaseVelocity);
                    Log.Debug("self.BaseVelocity * deltaTime: " + self.BaseVelocity * deltaTime);
                    Log.Debug("self.BaseVelocity * deltaTime * remainingMovementMagnitude: " + self.BaseVelocity * deltaTime * remainingMovementMagnitude);
                    Log.Debug("self.BaseVelocity * remainingMovementMagnitude: " + self.BaseVelocity * remainingMovementMagnitude);

                    self.BaseVelocity = self.BaseVelocity * magnitude;
                    Log.Debug("self.BaseVelocity = self.BaseVelocity * magnitude: " + self.BaseVelocity);
                }
                else
                {
                    Log.Debug("!self._solveMovementCollisions");
                    self._transientPosition += self.BaseVelocity * deltaTime;
                }
            }
            if (self.InteractiveRigidbodyHandling)
            {
                Log.Debug("self.InteractiveRigidbodyHandling");
                self.ProcessVelocityForRigidbodyHits(ref self.BaseVelocity, deltaTime);
                Log.Debug("self.BaseVelocity: " + self.BaseVelocity);
            }

            if (self.HasPlanarConstraint)
            {
                Log.Debug("self.HasPlanarConstraint");
                self._transientPosition = self._initialSimulationPosition + Vector3.ProjectOnPlane(self._transientPosition - self._initialSimulationPosition, self.PlanarConstraintAxis.normalized);
            }

            if (self.DiscreteCollisionEvents)
            {
                Log.Debug("self.DiscreteCollisionEvents");
                int num = self.CharacterCollisionsOverlap(self._transientPosition, self._transientRotation, self._internalProbedColliders, 0.02f);
                for (int index = 0; index < num; ++index)
                {
                    self.CharacterController.OnDiscreteCollisionDetected(self._internalProbedColliders[index]);
                }
            }
        }
        else
        {
            Log.Debug("!doExpensive");
            self.CharacterController.UpdateVelocity(ref self.BaseVelocity, deltaTime);
            Log.Debug("self.BaseVelocity: " + self.BaseVelocity);
            if ((double)self.BaseVelocity.magnitude < 0.00999999977648258)
            {
                Log.Debug("(double)self.BaseVelocity.magnitude < 0.00999999977648258");
                self.BaseVelocity = Vector3.zero;
            }

            if (self.InteractiveRigidbodyHandling)
            {
                Log.Debug("self.InteractiveRigidbodyHandling");
                self.ProcessVelocityForRigidbodyHits(ref self.BaseVelocity, deltaTime);
                Log.Debug("self.BaseVelocity: " + self.BaseVelocity);
            }

            self._transientPosition += self.BaseVelocity * deltaTime;
        }
        self.CharacterController.AfterCharacterUpdate(deltaTime);
    }

    float remainingMovementMagnitude;

    private bool KinematicCharacterMotor_InternalCharacterMove(KinematicCharacterMotor self, ref Vector3 transientVelocity, float deltaTime)
    {
        LogDebug("InternalCharacterMove");

        if ((double)deltaTime <= 0.0)
        {
            LogDebug("(double)deltaTime <= 0.0");
            return false;
        }

        bool flag1 = true;
        Vector3 normalized1 = transientVelocity.normalized;
        remainingMovementMagnitude = transientVelocity.magnitude * deltaTime;
        Vector3 originalDirection = normalized1;
        int num1 = 0;
        bool flag2 = true;
        Vector3 vector3_1 = self._transientPosition;
        bool previousHitIsStable = false;
        Vector3 previousVelocity = self._cachedZeroVector;
        Vector3 previousObstructionNormal = self._cachedZeroVector;
        MovementSweepState sweepState = MovementSweepState.Initial;
        for (int index = 0; index < self._overlapsCount; ++index)
        {
            Vector3 normal = self._overlaps[index].Normal;
            if ((double)Vector3.Dot(normalized1, normal) < 0.0)
            {
                LogDebug("(double)Vector3.Dot(normalized1, normal) < 0.0");
                bool stableOnHit = self.IsStableOnNormal(normal) && !self.MustUnground();
                Vector3 vector3_2 = transientVelocity;
                Vector3 obstructionNormal = self.GetObstructionNormal(normal, stableOnHit);
                self.InternalHandleVelocityProjection(stableOnHit, normal, obstructionNormal, originalDirection, ref sweepState, previousHitIsStable, previousVelocity, previousObstructionNormal, ref transientVelocity, ref remainingMovementMagnitude, ref normalized1);
                LogDebug("remainingMovementMagnitude: " + remainingMovementMagnitude);
                LogDebug("transientVelocity: " + transientVelocity);
                previousHitIsStable = stableOnHit;
                previousVelocity = vector3_2;
                previousObstructionNormal = obstructionNormal;
            }
        }
        while ((((double)remainingMovementMagnitude <= 0.0 ? 0 : (num1 <= 6 ? 1 : 0)) & (flag2 ? 1 : 0)) != 0)
        {
            bool flag3 = false;
            Vector3 hitPoint = new Vector3();
            Vector3 vector3_3 = new Vector3();
            float num2 = 0.0f;
            Collider hitCollider = (Collider)null;
            if (self.CheckMovementInitialOverlaps)
            {
                LogDebug("self.CheckMovementInitialOverlaps");
                int num3 = self.CharacterCollisionsOverlap(vector3_1, self._transientRotation, self._internalProbedColliders);
                if (num3 > 0)
                {
                    LogDebug("num3 > 0");
                    num2 = 0.0f;
                    float num4 = 2f;
                    for (int index = 0; index < num3; ++index)
                    {
                        Collider internalProbedCollider = self._internalProbedColliders[index];
                        Vector3 direction;
                        float distance;
                        if (Physics.ComputePenetration((Collider)self.Capsule, vector3_1, self._transientRotation, internalProbedCollider, internalProbedCollider.transform.position, internalProbedCollider.transform.rotation, out direction, out distance))
                        {
                            LogDebug("Physics.ComputePenetration((Collider)self.Capsule, vector3_1, self._transientRotation, internalProbedCollider, internalProbedCollider.transform.position, internalProbedCollider.transform.rotation, out direction, out distance)");
                            float num5 = Vector3.Dot(normalized1, direction);
                            if ((double)num5 < 0.0 && (double)num5 < (double)num4)
                            {
                                LogDebug("(double)num5 < 0.0 && (double)num5 < (double)num4");
                                num4 = num5;
                                vector3_3 = direction;
                                hitCollider = internalProbedCollider;
                                hitPoint = vector3_1 + self._transientRotation * self.CharacterTransformToCapsuleCenter + direction * distance;
                                flag3 = true;
                            }
                        }
                    }
                }
            }
            RaycastHit closestHit;
            if (!flag3 && self.CharacterCollisionsSweep(vector3_1, self._transientRotation, normalized1, remainingMovementMagnitude + 0.01f, out closestHit, self._internalCharacterHits) > 0)
            {
                LogDebug("!flag3 && self.CharacterCollisionsSweep(vector3_1, self._transientRotation, normalized1, remainingMovementMagnitude + 0.01f, out closestHit, self._internalCharacterHits) > 0");
                vector3_3 = closestHit.normal;
                num2 = closestHit.distance;
                hitCollider = closestHit.collider;
                hitPoint = closestHit.point;
                flag3 = true;
            }
            if (flag3)
            {
                LogDebug("flag3");
                Vector3 vector3_4 = normalized1 * Mathf.Max(0.0f, num2 - 0.01f);
                vector3_1 += vector3_4;
                remainingMovementMagnitude -= vector3_4.magnitude;
                LogDebug("remainingMovementMagnitude: " + remainingMovementMagnitude);
                HitStabilityReport hitStabilityReport = new HitStabilityReport();
                self.EvaluateHitStability(hitCollider, vector3_3, hitPoint, vector3_1, self._transientRotation, transientVelocity, ref hitStabilityReport);
                bool flag4 = false;
                if (self._solveGrounding && self.StepHandling != StepHandlingMethod.None && hitStabilityReport.ValidStepDetected && (double)Mathf.Abs(Vector3.Dot(vector3_3, self._characterUp)) <= 0.00999999977648258)
                {
                    LogDebug("self._solveGrounding && self.StepHandling != StepHandlingMethod.None && hitStabilityReport.ValidStepDetected && (double)Mathf.Abs(Vector3.Dot(vector3_3, self._characterUp)) <= 0.00999999977648258");
                    Vector3 normalized2 = Vector3.ProjectOnPlane(-vector3_3, self._characterUp).normalized;
                    Vector3 position = vector3_1 + normalized2 * 0.03f + self._characterUp * self.MaxStepHeight;
                    int num6 = self.CharacterCollisionsSweep(position, self._transientRotation, -self._characterUp, self.MaxStepHeight, out RaycastHit _, self._internalCharacterHits, acceptOnlyStableGroundLayer: true);
                    for (int index = 0; index < num6; ++index)
                    {
                        if ((UnityEngine.Object)self._internalCharacterHits[index].collider == (UnityEngine.Object)hitStabilityReport.SteppedCollider)
                        {
                            LogDebug("(UnityEngine.Object)self._internalCharacterHits[index].collider == (UnityEngine.Object)hitStabilityReport.SteppedCollider");
                            vector3_1 = position + -self._characterUp * (self._internalCharacterHits[index].distance - 0.01f);
                            flag4 = true;
                            transientVelocity = Vector3.ProjectOnPlane(transientVelocity, self.CharacterUp);
                            LogDebug("transientVelocity: " + transientVelocity);
                            normalized1 = transientVelocity.normalized;
                            LogDebug("normalized1: " + normalized1);
                            break;
                        }
                    }
                }
                if (!flag4)
                {
                    LogDebug("!flag4");
                    Vector3 obstructionNormal = self.GetObstructionNormal(vector3_3, hitStabilityReport.IsStable);
                    self.CharacterController.OnMovementHit(hitCollider, vector3_3, hitPoint, ref hitStabilityReport);
                    if (self.InteractiveRigidbodyHandling && (bool)(UnityEngine.Object)hitCollider.attachedRigidbody)
                    {
                        LogDebug("self.InteractiveRigidbodyHandling && (bool)(UnityEngine.Object)hitCollider.attachedRigidbody");
                        self.StoreRigidbodyHit(hitCollider.attachedRigidbody, transientVelocity, hitPoint, obstructionNormal, hitStabilityReport);
                    }

                    bool stableOnHit = hitStabilityReport.IsStable && !self.MustUnground();
                    Vector3 vector3_5 = transientVelocity;
                    self.InternalHandleVelocityProjection(stableOnHit, vector3_3, obstructionNormal, originalDirection, ref sweepState, previousHitIsStable, previousVelocity, previousObstructionNormal, ref transientVelocity, ref remainingMovementMagnitude, ref normalized1);
                    LogDebug("remainingMovementMagnitude: " + remainingMovementMagnitude);
                    LogDebug("transientVelocity: " + transientVelocity);
                    previousHitIsStable = stableOnHit;
                    previousVelocity = vector3_5;
                    previousObstructionNormal = obstructionNormal;
                }
            }
            else
            {
                LogDebug("flag2 = false;");
                flag2 = false;
            }

            ++num1;
            if (num1 > 6)
            {
                LogDebug("num1 > 6");
                if (self.KillRemainingMovementWhenExceedMaxMovementIterations)
                {
                    LogDebug("remainingMovementMagnitude = 0.0f");
                    remainingMovementMagnitude = 0.0f;
                }

                if (self.KillVelocityWhenExceedMaxMovementIterations)
                {
                    LogDebug("self.KillVelocityWhenExceedMaxMovementIterations");
                    transientVelocity = Vector3.zero;
                    LogDebug("transientVelocity: " + transientVelocity);
                }

                flag1 = false;
            }
        }


        self._transientPosition = vector3_1 + normalized1 * remainingMovementMagnitude;
        return flag1;

        void LogDebug(string message)
        {
            if (_tpContext && self.gameObject.name == "HuntressBody(Clone)")
            {
                Log.Debug(message);
                return;
            }
        }
    }
}