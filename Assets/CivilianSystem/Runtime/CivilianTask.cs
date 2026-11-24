// =====================================================
//  Civilian System for Unity
//  Author: Shaban
// =====================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace CivilianSystem
{
    public enum TaskType { MoveTo, Wait, PlayAnimation, PlaySound, FacePoint, Custom }

    /// <summary>
    /// One task step. Chain many = "multi-tasking" schedule (e.g. walk to shop,
    /// wait, play 'buy' anim, walk home).
    /// </summary>
    [Serializable]
    public class CivilianTask
    {
        public TaskType type = TaskType.MoveTo;
        public Transform target;               // MoveTo / FacePoint
        public Vector3 position;               // fallback if target null
        public float duration = 2f;            // Wait / PlayAnimation hold time
        public string animTrigger;             // PlayAnimation
        public AudioClip soundClip;            // PlaySound
        public UnityEvent onExecute;           // Custom
        [Tooltip("Higher runs first when queued with priority")]
        public int priority = 0;

        public Vector3 GetPosition() => target != null ? target.position : position;
    }

    /// <summary>
    /// Priority task queue per civilian. Brain pulls tasks when in Task state.
    /// Runtime API:
    ///   scheduler.Enqueue(task);
    ///   scheduler.EnqueueMoveTo(pos, priority);
    /// </summary>
    public class CivilianTaskScheduler : MonoBehaviour
    {
        [Tooltip("Tasks executed on spawn, in order (respecting priority)")]
        public List<CivilianTask> initialTasks = new List<CivilianTask>();
        [Tooltip("Restart initial task list when finished")]
        public bool loopInitialTasks = false;

        readonly List<CivilianTask> queue = new List<CivilianTask>();

        public bool HasTasks => queue.Count > 0;
        public event Action OnQueueEmptied;

        public void ResetToInitial()
        {
            queue.Clear();
            foreach (var t in initialTasks) Enqueue(t);
        }

        public void Enqueue(CivilianTask task)
        {
            // insert keeping descending priority, stable for equal priority
            int i = queue.FindIndex(t => t.priority < task.priority);
            if (i < 0) queue.Add(task);
            else queue.Insert(i, task);
        }

        public void EnqueueMoveTo(Vector3 pos, int priority = 0) =>
            Enqueue(new CivilianTask { type = TaskType.MoveTo, position = pos, priority = priority });

        public void EnqueueWait(float seconds, int priority = 0) =>
            Enqueue(new CivilianTask { type = TaskType.Wait, duration = seconds, priority = priority });

        public CivilianTask Dequeue()
        {
            if (queue.Count == 0) return null;
            var t = queue[0];
            queue.RemoveAt(0);
            if (queue.Count == 0)
            {
                if (loopInitialTasks && initialTasks.Count > 0)
                    foreach (var it in initialTasks) Enqueue(it);
                else
                    OnQueueEmptied?.Invoke();
            }
            return t;
        }

        public void ClearAll() => queue.Clear();
    }
}
