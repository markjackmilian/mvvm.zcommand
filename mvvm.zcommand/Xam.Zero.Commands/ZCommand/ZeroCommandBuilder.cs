using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Xam.Zero.Commands.ZCommand
{
    public class ZeroCommandBuilder<T>
    {
        private readonly INotifyPropertyChanged _viewmodel;
        private IEnumerable<string> _trackedProperties;
        private bool _swallowException;
        private Action<Exception> _onError;
        private Func<Exception, Task> _onErrorAsync;
        private Func<bool> _canExecute;
        private Action<T, ZeroCommandContext> _action;
        private Func<T, ZeroCommandContext, Task> _actionAsync;
        private Func<ZeroCommandContext, bool> _beforeExecute;
        private Func<ZeroCommandContext, Task<bool>> _beforeExecuteAsync;
        private Action<ZeroCommandContext> _afterExecute;
        private Func<ZeroCommandContext, Task> _afterExecuteAsync;
        private int _concurrentExecution;
        private bool _autoCanExecute;
        private Func<ZeroCommandContext, bool> _validate;
        private Func<ZeroCommandContext, Task<bool>> _validateAsync;
        private List<INotifyCollectionChanged> _observedCollection = new List<INotifyCollectionChanged>();
        private List<INotifyPropertyChanged> _observedDependencies = new List<INotifyPropertyChanged>();

        internal ZeroCommandBuilder(INotifyPropertyChanged viewmodel)
        {
            this._viewmodel = viewmodel;
            this._concurrentExecution = 1;
        }

        /// <summary>
        /// Add observablecollection dependencies
        /// When a collection raise event canexecute is evaualted again
        /// You can add many collection to observe
        /// </summary>
        /// <param name="observableCollection"></param>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithRaiseCanExecuteOnCollectionChanged(INotifyCollectionChanged observableCollection)
        {
            this._observedCollection.Add(observableCollection);
            return this;
        }
        
        /// <summary>
        /// Evaluate can execute when a propertychanged event is raised from a tracked dependency
        /// </summary>
        /// <param name="propertyChanged"></param>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithDependencyEvaluate(INotifyPropertyChanged propertyChanged)
        {
            this._observedDependencies.Add(propertyChanged);
            return this;
        }

        /// <summary>
        /// Add can execute expression
        /// This expression is used for evalauation of canexecute and
        /// for tracking raise canexecute dependencies 
        /// </summary>
        /// <param name="canExcecuteExpression"></param>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithCanExecute(Expression<Func<bool>> canExcecuteExpression)
        {
            this._canExecute = canExcecuteExpression.Compile();
            this._trackedProperties = this.GetTrackProperties(canExcecuteExpression.Body, this._viewmodel.GetType());
            return this;
        }

        /// <summary>
        /// When a validator is created zerocommand will check this func before execute
        /// Validator not affect on canexecute
        /// </summary>
        /// <param name="validate"></param>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithValidator(Func<ZeroCommandContext,bool> validate)
        {
            if (this._validate != null || this._validateAsync != null)
                throw new Exception("Validate action already added!");

            this._validate = validate;
            return this;
        }

        /// <summary>
        /// When a validator is created zerocommand will check this func before execute
        /// Validator not affect on canexecute
        /// </summary>
        /// <param name="validate"></param>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithValidator(Func<ZeroCommandContext,Task<bool>> validate)
        {
            if (this._validate != null || this._validateAsync != null)
                throw new Exception("Validate action already added!");
            
            this._validateAsync = validate;
            return this;
        }


        /// <summary>
        /// Retrieve track properties that exist on tracked object
        /// </summary>
        /// <param name="canExcecuteExpression"></param>
        /// <param name="trackedType"></param>
        /// <returns></returns>
        private IEnumerable<string> GetTrackProperties(Expression canExcecuteExpression, Type trackedType)
        {
            var allProperties = new List<string>();

            switch (canExcecuteExpression)
            {
                case MemberExpression memberExpression:
                {
                    if (memberExpression.Member.DeclaringType == trackedType)
                        allProperties.Add(memberExpression.Member.Name);
                    break;
                }
                case BinaryExpression binaryExpression:
                    allProperties.AddRange(this.GetTrackProperties(binaryExpression.Left, trackedType));
                    allProperties.AddRange(this.GetTrackProperties(binaryExpression.Right, trackedType));
                    break;
                case MethodCallExpression methodCallExpression:
                    foreach (var expression in methodCallExpression.Arguments)
                    {
                        allProperties.AddRange(this.GetTrackProperties(expression, trackedType));
                    }

                    break;
                case UnaryExpression unaryExpression:
                    allProperties.AddRange(this.GetTrackProperties(unaryExpression.Operand, trackedType));
                    break;
            }

            return allProperties.Distinct();
        }


        /// <summary>
        /// How many concurrent execution are supported.
        /// Default is 1
        /// </summary>
        /// <param name="concurrentExecution"></param>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithConcurrentExecutionOf(int concurrentExecution)
        {
            this._concurrentExecution = concurrentExecution;
            return this;
        }


        /// <summary>
        /// Do not throw exception on execute
        /// </summary>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithSwallowException()
        {
            this._swallowException = true;
            return this;
        }

        /// <summary>
        /// Catch execution error
        /// </summary>
        /// <param name="onError"></param>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithErrorHandler(Action<Exception> onError)
        {
            if (this._onErrorAsync != null || this._onError != null)
                throw new Exception("On error action already added!");

            this._onError = onError;
            return this;
        }

        /// <summary>
        /// Catch execution error
        /// </summary>
        /// <param name="onErrorTask"></param>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithErrorHandler(Func<Exception, Task> onErrorTask)
        {
            if (this._onErrorAsync != null || this._onError != null)
                throw new Exception("On error action already added!");

            this._onErrorAsync = onErrorTask;
            return this;
        }

        /// <summary>
        /// Add Execute for Action
        /// </summary>
        /// <param name="action"></param>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithExecute(Action<T, ZeroCommandContext> action)
        {
            if (this._action != null || this._actionAsync != null)
                throw new Exception("Execute action already added!");

            this._action = action;
            return this;
        }

        /// <summary>
        /// Add Execute for Task
        /// </summary>
        /// <param name="taskAction"></param>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithExecute(Func<T, ZeroCommandContext, Task> taskAction)
        {
            if (this._action != null || this._actionAsync != null)
                throw new Exception("Execute action already added!");

            this._actionAsync = taskAction;
            return this;
        }


        /// <summary>
        /// Add action before execute
        /// </summary>
        /// <param name="beforeExecute"></param>
        /// <returns>If return false stop the execution</returns>
        public ZeroCommandBuilder<T> WithBeforeExecute(Func<ZeroCommandContext, bool> beforeExecute)
        {
            if (this._beforeExecute != null || this._beforeExecuteAsync != null)
                throw new Exception("Before Execute action already added!");

            this._beforeExecute = beforeExecute;
            return this;
        }

        /// <summary>
        /// Add action before execute
        /// </summary>
        /// <param name="beforeExecuteAsync"></param>
        /// <returns>If return false stop the execution</returns>
        public ZeroCommandBuilder<T> WithBeforeExecute(Func<ZeroCommandContext, Task<bool>> beforeExecuteAsync)
        {
            if (this._beforeExecute != null || this._beforeExecuteAsync != null)
                throw new Exception("Before Execute action already added!");

            this._beforeExecuteAsync = beforeExecuteAsync;
            return this;
        }


        /// <summary>
        /// Add action after execute (runned in finally scope)
        /// </summary>
        /// <param name="afterExecute"></param>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithAfterExecute(Action<ZeroCommandContext> afterExecute)
        {
            if (this._afterExecute != null || this._afterExecuteAsync != null)
                throw new Exception("Before Execute action already added!");

            this._afterExecute = afterExecute;
            return this;
        }

        /// <summary>
        /// Add action after execute (runned in finally scope)
        /// </summary>
        /// <param name="afterExecuteAsync"></param>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithAfterExecute(Func<ZeroCommandContext, Task> afterExecuteAsync)
        {
            if (this._afterExecute != null || this._afterExecuteAsync != null)
                throw new Exception("Before Execute action already added!");

            this._afterExecuteAsync = afterExecuteAsync;
            return this;
        }

        /// <summary>
        /// If AutoCanExecute is enabled command canexecute is false when is executing
        /// </summary>
        /// <returns></returns>
        public ZeroCommandBuilder<T> WithAutoInvalidateWhenExecuting()
        {
            this._autoCanExecute = true;
            return this;
        }

        /// <summary>
        /// Create a new ZeroCommand instance
        /// </summary>
        /// <returns></returns>
        public ZeroCommand<T> Build()
        {
            return new ZeroCommand<T>(this._viewmodel, this._action, this._actionAsync, this._canExecute, this._onError,
                this._onErrorAsync, this._swallowException, this._trackedProperties, this._beforeExecute,
                this._beforeExecuteAsync, this._afterExecute, this._afterExecuteAsync, this._concurrentExecution,
                this._autoCanExecute, this._validate, this._validateAsync, this._observedCollection, this._observedDependencies);
        }
    }
}