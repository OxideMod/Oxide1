using System;

using UnityEngine;

namespace Oxide
{
    /// <summary>
    /// Represents a timer that fires a callback after a specific delay
    /// </summary>
    public class Timer
    {
        /// <summary>
        /// The number of iterations remaining on this timer
        /// </summary>
        public int IterationsRemaining { get; private set; }
        /// <summary>
        /// The delay between each iteration in seconds
        /// </summary>
        public float Delay { get; private set; }
        /// <summary>
        /// The callback to raise upon each iteration
        /// </summary>
        public Action Callback { get; private set; }

        private float createtime, nextiteration;
        private bool isfinished;

        /// <summary>
        /// Raised when the timer finishes
        /// </summary>
        public event Action<Timer> OnFinished;

        private Timer(int iterations, float delay)
        {
            // Store parameters
            IterationsRemaining = iterations;
            Delay = delay;

            // Store needed timestamps
            createtime = Time.realtimeSinceStartup;
            nextiteration = createtime + Delay;
        }

        /// <summary>
        /// Updates this timer
        /// </summary>
        public void Update()
        {
            // Sanity check
            if (isfinished) return;

            // Is it time to fire an iteration off?
            float time = Time.realtimeSinceStartup;
            if (time >= nextiteration)
            {
                // Calculate next iteration
                nextiteration = time + Delay;
                if (IterationsRemaining > 0)
                {
                    IterationsRemaining--;
                    if (IterationsRemaining == 0) Destroy();
                }

                // Raise callback
                //Debug.Log("Timer firing");
                Callback();
            }
        }

        /// <summary>
        /// Finishes this timer
        /// </summary>
        public void Destroy()
        {
            // Sanity check
            if (isfinished) return;

            // We're done
            isfinished = true;
            if (OnFinished != null) OnFinished(this);
        }

        #region Static Interface

        /// <summary>
        /// Creates a time that will fire for a fixed number of iterations and then finish
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="numiterations"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public static Timer Create(float delay, int numiterations, Action callback)
        {
            Timer result = new Timer(numiterations, delay);
            result.Callback = callback;
            return result;
        }
        /// <summary>
        /// Create a timer that will fire indefinetely
        /// </summary>
        /// <param name="delay"></param>
        /// <param name="callback"></param>
        /// <returns></returns>
        public static Timer Create(float delay, Action callback)
        {
            Timer result = new Timer(0, delay);
            result.Callback = callback;
            return result;
        }

        #endregion

    }
}
