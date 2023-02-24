### mvvm zCommand
This is a porting of Xam.Zero.ZCommand as a separate project.
The goal is to keep the ViewModel as clean as possible by **automatically tracking dependencies** and composing a flow related to the execution of the command.
You can find usage examples in "CommandPageViewModel" in this repo. (coming soon)

You can create instances of ICommand using the ZeroComandBuilder and you can customize the flow of the activity in a descriptive way.

#### Dependency tracker
When you create a zerocommand you have to specify an INotifyPropertyChanged instance (usually the viewmodel) so that the CanExecute expression is evaluated by finding properties that exist on the viewmodel in order to re-evaluate the canexecute automatically.
Example:

     this.TestSuccessCommand = ZeroCommand.On(this)
                .WithCanExecute(()=> !this.IsBusy && !string.IsNullOrEmpty(this.Name))
                .WithExecute((commandParam, context) => this.InnerShowMessageAction())
                .Build();

So CanExecute on this ICommand is automatically evaluated when IsBusy or SomeProperty changed (all tracked dependencies must be implement propertychanged)

You can add a dependency on a observablecollection using ".WithRaiseCanExecuteOnCollectionChanged", when the collection changed the canexecute expressio will be evaluated again.

**Error Handler**

Is possible to intercept exceptions in order to keep the executor method as clean as possible:

      this.TestErrorCommand = ZeroCommand.On(this)
                .WithCanExecute(this.InnerExpression())
                .WithExecute((commandParam, context) => this.InnerManageErrorWithoutSwallow())
                .WithErrorHandler(exception => base.DisplayAlert("Managed Exception", exception.Message, "ok"))
                .Build();

**Before and After Execute**

Is possibile to run some logic before execute and after execute. If before execute return false it will stop the execution.


            this.BeforeRunEvaluationCommadn = ZeroCommand.On(this)
                .WithCanExecute(this.InnerExpression())
                .WithExecute((commandParam, context) => this.InnerEvaluateCanRun())
                .WithBeforeExecute(context => base.DisplayAlert("Before Run Question", "Can i run?", "yes", "no"))
                .WithAfterExecute(context =>
                    base.DisplayAlert("I'm running after a execution", "I'll not run if evaluation fail!", "ok"))
                .Build();


You can pass data between flow step (before, execute, after) using context:

    this.ContextEvaluationCommand = ZeroCommand.On(this)
                .WithCanExecute(this.InnerExpression())
                .WithExecute((commandParam, context) => this.InnerShowMessageAction())
                .WithBeforeExecute(context =>
                {
                    var stopWatch = new Stopwatch();
                    context.Add(stopWatch);
                    stopWatch.Start();
                    return true;
                })
                .WithAfterExecute(async context =>
                {
                    var stopWatch = context.Get<Stopwatch>();
                    stopWatch.Stop();
                    await this.DisplayAlert("Evaluation", $"Executed in {stopWatch.ElapsedMilliseconds} ms", "OK");
                    stopWatch.Reset();
                })
                .Build();

**Auto Invalidate Command**

ZeroCommand can autoinvalidate itself during execution so it auto prevent double-tap button.

    this.AutoInvalidateCommand = ZeroCommand.On(this)
                .WithAutoInvalidateWhenExecuting()
                .WithExecute(async (o, context) =>
                {
                    await Task.Delay(1000);
                    await base.DisplayAlert("Auto invalidate", "Now button should be disabled!", "ok");
                }).Build();

**Concurrent Execution**

You can set how many concurrent execution could support:
`.WithConcurrentExecutionOf(3)`
where 3 is the number of concurrent execution (default value is 1)
