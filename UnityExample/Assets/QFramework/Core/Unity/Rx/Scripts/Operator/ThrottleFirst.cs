﻿/****************************************************************************
 * Copyright (c) 2017 liangxie
 * 
 * http://liangxiegame.com
 * https://github.com/liangxiegame/QFramework
 * https://github.com/liangxiegame/QSingleton
 * https://github.com/liangxiegame/QChain
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 ****************************************************************************/

namespace QFramework
{
    using System;

    public class ThrottleFirstObservable<T> : OperatorObservableBase<T>
    {
        readonly IObservable<T> source;
        readonly TimeSpan dueTime;
        readonly IScheduler scheduler;

        public ThrottleFirstObservable(IObservable<T> source, TimeSpan dueTime,IScheduler scheduler) 
            : base(scheduler == Scheduler.CurrentThread || source.IsRequiredSubscribeOnCurrentThread())
        {
            this.source = source;
            this.dueTime = dueTime;
            this.scheduler = scheduler;
        }

        protected override IDisposable SubscribeCore(IObserver<T> observer, IDisposable cancel)
        {
            return new ThrottleFirst(this, observer, cancel).Run();
        }

        class ThrottleFirst : OperatorObserverBase<T, T>
        {
            readonly ThrottleFirstObservable<T> parent;
            readonly object gate = new object();
            bool open = true;
            SerialDisposable cancelable;

            public ThrottleFirst(ThrottleFirstObservable<T> parent, IObserver<T> observer, IDisposable cancel) : base(observer, cancel)
            {
                this.parent = parent;
            }

            public IDisposable Run()
            {
                cancelable = new SerialDisposable();
                var subscription = parent.source.Subscribe(this);

                return StableCompositeDisposable.Create(cancelable, subscription);
            }

            void OnNext()
            {
                lock (gate)
                {
                    open = true;
                }
            }

            public override void OnNext(T value)
            {
                lock (gate)
                {
                    if (!open) return;
                    observer.OnNext(value);
                    open = false;
                }

                var d = new SingleAssignmentDisposable();
                cancelable.Disposable = d;
                d.Disposable = parent.scheduler.Schedule(parent.dueTime, OnNext);
            }

            public override void OnError(Exception error)
            {
                cancelable.Dispose();

                lock (gate)
                {
                    try { observer.OnError(error); } finally { Dispose(); }
                }
            }

            public override void OnCompleted()
            {
                cancelable.Dispose();

                lock (gate)
                {
                    try { observer.OnCompleted(); } finally { Dispose(); }
                }
            }
        }
    }
}