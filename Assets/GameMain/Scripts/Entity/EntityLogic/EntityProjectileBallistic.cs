﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityGameFramework.Runtime;
using Flower.Data;

namespace Flower
{
    public class EntityProjectileBallistic : EntityHideSelfProjectile
    {
        public BallisticArcHeight arcPreference;
        public BallisticFireMode fireMode;
        [Range(-90, 90)]
        public float firingAngle;
        public float startSpeed;

        public LayerMask mask = -1;

        /// <summary>
        /// The duration that collisions between this gameObjects colliders
        /// and the given colliders will be ignored.
        /// </summary>
        public float collisionIgnoreTime = 0.35f;

        private bool m_Fired, m_IgnoringCollsions;
        private float m_CollisionIgnoreCount = 0;
        private Rigidbody m_Rigidbody;
        private List<Collider> m_CollidersIgnoring = new List<Collider>();

        /// <summary>
        /// All the colliders attached to this gameObject and its children
        /// </summary>
        private Collider[] m_Colliders;

        static readonly Collider[] s_Enemies = new Collider[64];

        private Vector3 tempVelocity;

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            m_Rigidbody = GetComponent<Rigidbody>();
            m_Colliders = GetComponentsInChildren<Collider>();
        }

        protected override void OnShow(object userData)
        {
            base.OnShow(userData);



            Vector3 startPosition = entityDataProjectile.FiringPoint.position;
            Vector3 targetPoint;
            if (fireMode == BallisticFireMode.UseLaunchSpeed)
            {
                // use speed
                targetPoint = Ballistics.CalculateBallisticLeadingTargetPointWithSpeed(
                    startPosition,
                    entityDataProjectile.EntityTargetable.transform.position, entityDataProjectile.EntityTargetable.Velocity,
                    startSpeed, arcPreference, Physics.gravity.y, 4);
            }
            else
            {
                // use angle
                targetPoint = Ballistics.CalculateBallisticLeadingTargetPointWithAngle(
                    startPosition,
                    entityDataProjectile.EntityTargetable.transform.position, entityDataProjectile.EntityTargetable.Velocity, firingAngle,
                    arcPreference, Physics.gravity.y, 4);
            }

            FireAtPoint(startPosition, targetPoint);
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);

            if (pause)
                return;

            if (!m_Fired)
            {
                return;
            }
            // If we are ignoring collisions, increment counter. 
            // If counter is complete, reenable collisions
            if (m_IgnoringCollsions)
            {
                m_CollisionIgnoreCount += Time.deltaTime;
                if (m_CollisionIgnoreCount >= collisionIgnoreTime)
                {
                    m_IgnoringCollsions = false;
                    foreach (Collider colliderIgnoring in m_CollidersIgnoring)
                    {
                        foreach (Collider projectileCollider in m_Colliders)
                        {
                            Physics.IgnoreCollision(colliderIgnoring, projectileCollider, false);
                        }
                    }
                    m_CollidersIgnoring.Clear();
                }
            }

            transform.rotation = Quaternion.LookRotation(m_Rigidbody.linearVelocity);
        }

        protected override void OnHide(bool isShutdown, object userData)
        {
            base.OnHide(isShutdown, userData);

            m_Fired = m_IgnoringCollsions = false;
            m_CollisionIgnoreCount = 0;
            m_CollidersIgnoring.Clear();

            tempVelocity = Vector3.zero;
        }

        /// <summary>
        /// Fires this projectile from a designated start point to a designated world coordinate.
        /// Automatically sets firing angle to suit launch speed unless angle is overridden, in which case launch speed is overridden to suit angle.
        /// </summary>
        /// <param name="startPoint">Start point of the flight.</param>
        /// <param name="targetPoint">Target point to fly to.</param>
        public virtual void FireAtPoint(Vector3 startPoint, Vector3 targetPoint)
        {
            transform.position = startPoint;

            Vector3 firingVector;

            switch (fireMode)
            {
                case BallisticFireMode.UseLaunchSpeed:
                    firingVector =
                        Ballistics.CalculateBallisticFireVectorFromVelocity(startPoint, targetPoint, startSpeed, arcPreference);
                    firingAngle = Ballistics.CalculateBallisticFireAngle(startPoint, targetPoint, startSpeed, arcPreference);
                    break;
                case BallisticFireMode.UseLaunchAngle:
                    firingVector = Ballistics.CalculateBallisticFireVectorFromAngle(startPoint, targetPoint, firingAngle);
                    startSpeed = firingVector.magnitude;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            Fire(firingVector);
        }

        /// <summary>
        /// Fires this projectile in a designated direction at the launch speed.
        /// </summary>
        /// <param name="startPoint">Start point of the flight.</param>
        /// <param name="fireVector">Vector representing launch direction.</param>
        public virtual void FireInDirection(Vector3 startPoint, Vector3 fireVector)
        {
            transform.position = startPoint;

            Fire(fireVector.normalized * startSpeed);
        }

        /// <summary>
        /// Fires this projectile at a designated starting velocity, overriding any starting speeds.
        /// </summary>
        /// <param name="startPoint">Start point of the flight.</param>
        /// <param name="fireVelocity">Vector3 representing launch velocity.</param>
        public void FireAtVelocity(Vector3 startPoint, Vector3 fireVelocity)
        {
            transform.position = startPoint;

            startSpeed = fireVelocity.magnitude;

            Fire(fireVelocity);
        }

        /// <summary>
        /// Ignores all collisions between this and the given colliders for a defined period of time
        /// </summary>
        /// <param name="collidersToIgnore">Colliders to ignore</param>
        public void IgnoreCollision(Collider[] collidersToIgnore)
        {
            if (collisionIgnoreTime > 0)
            {
                m_IgnoringCollsions = true;
                m_CollisionIgnoreCount = 0.0f;
                foreach (Collider colliderToIgnore in collidersToIgnore)
                {
                    if (m_CollidersIgnoring.Contains(colliderToIgnore))
                    {
                        continue;
                    }
                    foreach (Collider projectileCollider in m_Colliders)
                    {
                        Physics.IgnoreCollision(colliderToIgnore, projectileCollider, true);
                    }
                    m_CollidersIgnoring.Add(colliderToIgnore);
                }
            }
        }

        protected virtual void Fire(Vector3 firingVector)
        {
            transform.rotation = Quaternion.LookRotation(firingVector);

            m_Rigidbody.linearVelocity = firingVector;

            m_Fired = true;

            m_CollidersIgnoring.Clear();
        }

        void OnTriggerEnter(Collider other)
        {
            EntityEnemy enemy = other.gameObject.GetComponent<EntityEnemy>();
            if (enemy == null)
                return;

            if (!enemy.IsDead)
                enemy.Damage(entityDataProjectile.ProjectileData.Damage);

            int number = Physics.OverlapSphereNonAlloc(transform.position, entityDataProjectile.ProjectileData.SplashRange, s_Enemies, mask);
            for (int index = 0; index < number; index++)
            {
                Collider collider = s_Enemies[index];
                var rangeEnemy = collider.GetComponent<EntityEnemy>();
                if (rangeEnemy == null)
                {
                    continue;
                }
                if (!enemy.IsDead)
                    rangeEnemy.Damage(entityDataProjectile.ProjectileData.SplashDamage);
            }

            SpawnCollisionParticles();

            if (!hide)
            {
                GameEntry.Event.Fire(this, HideEntityInLevelEventArgs.Create(Entity.Id));
                hide = true;
            }
        }

        public override void Pause()
        {
            base.Pause();
            tempVelocity = m_Rigidbody.linearVelocity;
            m_Rigidbody.linearVelocity = Vector3.zero;
            m_Rigidbody.isKinematic = true;
        }

        public override void Resume()
        {
            base.Resume();
            m_Rigidbody.linearVelocity = tempVelocity;
            m_Rigidbody.isKinematic = false;
        }

#if UNITY_EDITOR
        protected virtual void OnValidate()
        {
            if (Mathf.Abs(firingAngle) >= 90f)
            {
                firingAngle = Mathf.Sign(firingAngle) * 89.5f;
                Debug.LogWarning("Clamping angle to under +- 90 degrees to avoid errors.");
            }
        }
#endif
    }
}