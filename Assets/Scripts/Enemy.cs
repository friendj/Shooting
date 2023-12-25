using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class Enemy : LivingEntity
{
    protected enum State { Idel, Chasing, Attacking};
    protected State currentState;

    public float refreshRate = 0.25f;
    public ParticleSystem deathEffect;

    protected NavMeshAgent navMeshAgent;
    protected Transform target;
    protected LivingEntity targetEntity;

    protected float attackDis = .5f;
    protected float attackBetweenAttacks = 1f;

    protected float nextAttackTime;
    protected float myCollisionRadius;
    protected float targetCollisionRadius;
    protected float damage = 1;

    protected Material skinMaterial;
    protected Color originalColor;

    protected Renderer renderer;
    static protected Material baseMat;
    static protected Material attackMat;

    protected bool hasTarget = false;

    public static event System.Action EventEnemyDeath;

    protected override void Start()
    {
        base.Start();

        currentState = State.Chasing;

        navMeshAgent = GetComponent<NavMeshAgent>();
        myCollisionRadius = GetComponent<CapsuleCollider>().radius;
        skinMaterial = GetComponent<Renderer>().material;
        originalColor = skinMaterial.color;

        renderer = GetComponent<Renderer>();
        if (baseMat == null)
            baseMat = renderer.material;
        if (attackMat == null)
        {
            attackMat = new Material(baseMat);
            attackMat.color = Color.red;
        }

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
            targetEntity = player.GetComponent<LivingEntity>();
            targetEntity.EventOnDeath += OnTargetDeath;
            hasTarget = true;
            targetCollisionRadius = target.GetComponent<CapsuleCollider>().radius;
            StartCoroutine(FollowTarget());
        }
    }

    void OnTargetDeath()
    {
        hasTarget = false;
        currentState = State.Idel;
    }

    protected virtual void Update()
    {
        if (hasTarget)
        {
            if (Time.time > nextAttackTime)
            {
                float sqrDstToTarget = (transform.position - target.position).sqrMagnitude;
                if (sqrDstToTarget < Mathf.Pow(attackDis + myCollisionRadius + targetCollisionRadius, 2))
                {
                    nextAttackTime = Time.time + attackBetweenAttacks;
                    StopCoroutine("Attack");
                    StartCoroutine("Attack");
                    Game.Instance.AudioManager.PlaySound("EnemyAttack", transform.position);
                }
            }
        }
    }

    protected virtual IEnumerator Attack()
    {
        navMeshAgent.enabled = false;
        currentState = State.Attacking;
        Vector3 originalPosition = transform.position;
        Vector3 targetPosition = GetTargetPos(-attackDis / 2);

        float attackSpeed = 3f;
        float percent = 0;
        bool hasAppliedDamage = false;

        skinMaterial.color = Color.red;
        renderer.material = attackMat;
        while (percent < 1)
        {
            if (percent > .5f && !hasAppliedDamage)
            {
                hasAppliedDamage = true;
                targetEntity.TakeDamage(damage);
            }

            percent += Time.deltaTime * attackSpeed;
            float interpolation = (-Mathf.Pow(percent, 2) + percent) * 4;
            transform.position = Vector3.Lerp(originalPosition, targetPosition, interpolation);
            yield return null;
        }
        skinMaterial.color = originalColor;
        renderer.material = baseMat;

        navMeshAgent.enabled = true;
        currentState = State.Chasing;
    }

    IEnumerator FollowTarget()
    {
        while (hasTarget)
        {
            if (isDead)
                yield break;
            if (currentState == State.Chasing)
            {
                navMeshAgent.SetDestination(GetTargetPos(attackDis / 2));
            }
            yield return new WaitForSeconds(refreshRate);
        }
    }

    protected Vector3 GetTargetPos(float dis=0f)
    {
        return target.position - (target.position - transform.position).normalized * (targetCollisionRadius + myCollisionRadius + dis);
    }

    public override void TakeHit(float damage, Vector3 hitPoint, Vector3 hitDirection)
    {
        Debug.Log("Take hit");
        Game.Instance.AudioManager.PlaySound("Impact", transform.position);
        if (damage >= health)
        {
            //Debug.Log("Create deathEffect, " + hitDirection + " , " + transform.forward);
            if (EventEnemyDeath != null)
                EventEnemyDeath();
            Game.Instance.AudioManager.PlaySound("EnemyDead", transform.position);
            Destroy(Instantiate(deathEffect.gameObject, hitPoint, Quaternion.FromToRotation(Vector3.forward, hitDirection)), deathEffect.startLifetime);
        }
        base.TakeHit(damage, hitPoint, hitDirection);
    }
}
