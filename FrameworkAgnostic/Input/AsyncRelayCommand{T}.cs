using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FrameworkAgnostic.ComponentModel.__Internals;
using FrameworkAgnostic.Input.Interfaces;
using FrameworkAgnostic.Input.Internals;
using ArgumentNullException = FrameworkAgnostic.Properties.Polyfills.ArgumentNullException;

#pragma warning disable CS0618, CA1001

namespace FrameworkAgnostic.Input;

/// <summary>
///     A generic command that provides a more specific version of <see cref="AsyncRelayCommand" />.
/// </summary>
/// <typeparam name="T">The type of parameter being passed as input to the callbacks.</typeparam>
public sealed class AsyncRelayCommand<T> : IAsyncRelayCommand<T>, ICancellationAwareCommand
{
    /// <summary>
    ///     The cancelable <see cref="Func{TResult}" /> to invoke when <see cref="Execute(object?)" /> is used.
    /// </summary>
    private readonly Func<T?, CancellationToken, Task>? cancelableExecute;

    /// <summary>
    ///     The optional action to invoke when <see cref="CanExecute(T)" /> is used.
    /// </summary>
    private readonly Predicate<T?>? canExecute;

    /// <summary>
    ///     The <see cref="Func{TResult}" /> to invoke when <see cref="Execute(T)" /> is used.
    /// </summary>
    private readonly Func<T?, Task>? execute;

    /// <summary>
    ///     The options being set for the current command.
    /// </summary>
    private readonly AsyncRelayCommandOptions options;

    /// <summary>
    ///     The <see cref="CancellationTokenSource" /> instance to use to cancel <see cref="cancelableExecute" />.
    /// </summary>
    private CancellationTokenSource? cancellationTokenSource;

    private Task? executionTask;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncRelayCommand{T}" /> class.
    /// </summary>
    /// <param name="execute">The execution logic.</param>
    /// <remarks>See notes in <see cref="RelayCommand{T}(Action{T})" />.</remarks>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="execute" /> is <see langword="null" />.</exception>
    public AsyncRelayCommand(Func<T?, Task> execute)
    {
        ArgumentNullException.ThrowIfNull(execute);

        this.execute = execute;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncRelayCommand{T}" /> class.
    /// </summary>
    /// <param name="execute">The execution logic.</param>
    /// <param name="options">The options to use to configure the async command.</param>
    /// <remarks>See notes in <see cref="RelayCommand{T}(Action{T})" />.</remarks>
    /// <exception cref="System.ArgumentNullException">Thrown if <paramref name="execute" /> is <see langword="null" />.</exception>
    public AsyncRelayCommand(Func<T?, Task> execute, AsyncRelayCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(execute);

        this.execute = execute;
        this.options = options;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncRelayCommand{T}" /> class.
    /// </summary>
    /// <param name="cancelableExecute">The cancelable execution logic.</param>
    /// <remarks>See notes in <see cref="RelayCommand{T}(Action{T})" />.</remarks>
    /// <exception cref="System.ArgumentNullException">
    ///     Thrown if <paramref name="cancelableExecute" /> is
    ///     <see langword="null" />.
    /// </exception>
    public AsyncRelayCommand(Func<T?, CancellationToken, Task> cancelableExecute)
    {
        ArgumentNullException.ThrowIfNull(cancelableExecute);

        this.cancelableExecute = cancelableExecute;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncRelayCommand{T}" /> class.
    /// </summary>
    /// <param name="cancelableExecute">The cancelable execution logic.</param>
    /// <param name="options">The options to use to configure the async command.</param>
    /// <remarks>See notes in <see cref="RelayCommand{T}(Action{T})" />.</remarks>
    /// <exception cref="System.ArgumentNullException">
    ///     Thrown if <paramref name="cancelableExecute" /> is
    ///     <see langword="null" />.
    /// </exception>
    public AsyncRelayCommand(Func<T?, CancellationToken, Task> cancelableExecute, AsyncRelayCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(cancelableExecute);

        this.cancelableExecute = cancelableExecute;
        this.options = options;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncRelayCommand{T}" /> class.
    /// </summary>
    /// <param name="execute">The execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    /// <remarks>See notes in <see cref="RelayCommand{T}(Action{T})" />.</remarks>
    /// <exception cref="System.ArgumentNullException">
    ///     Thrown if <paramref name="execute" /> or <paramref name="canExecute" />
    ///     are <see langword="null" />.
    /// </exception>
    public AsyncRelayCommand(Func<T?, Task> execute, Predicate<T?> canExecute)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(canExecute);

        this.execute = execute;
        this.canExecute = canExecute;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncRelayCommand{T}" /> class.
    /// </summary>
    /// <param name="execute">The execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    /// <param name="options">The options to use to configure the async command.</param>
    /// <remarks>See notes in <see cref="RelayCommand{T}(Action{T})" />.</remarks>
    /// <exception cref="System.ArgumentNullException">
    ///     Thrown if <paramref name="execute" /> or <paramref name="canExecute" />
    ///     are <see langword="null" />.
    /// </exception>
    public AsyncRelayCommand(Func<T?, Task> execute, Predicate<T?> canExecute, AsyncRelayCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(canExecute);

        this.execute = execute;
        this.canExecute = canExecute;
        this.options = options;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncRelayCommand{T}" /> class.
    /// </summary>
    /// <param name="cancelableExecute">The cancelable execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    /// <remarks>See notes in <see cref="RelayCommand{T}(Action{T})" />.</remarks>
    /// <exception cref="System.ArgumentNullException">
    ///     Thrown if <paramref name="cancelableExecute" /> or
    ///     <paramref name="canExecute" /> are <see langword="null" />.
    /// </exception>
    public AsyncRelayCommand(Func<T?, CancellationToken, Task> cancelableExecute, Predicate<T?> canExecute)
    {
        ArgumentNullException.ThrowIfNull(cancelableExecute);
        ArgumentNullException.ThrowIfNull(canExecute);

        this.cancelableExecute = cancelableExecute;
        this.canExecute = canExecute;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AsyncRelayCommand{T}" /> class.
    /// </summary>
    /// <param name="cancelableExecute">The cancelable execution logic.</param>
    /// <param name="canExecute">The execution status logic.</param>
    /// <param name="options">The options to use to configure the async command.</param>
    /// <remarks>See notes in <see cref="RelayCommand{T}(Action{T})" />.</remarks>
    /// <exception cref="System.ArgumentNullException">
    ///     Thrown if <paramref name="cancelableExecute" /> or
    ///     <paramref name="canExecute" /> are <see langword="null" />.
    /// </exception>
    public AsyncRelayCommand(Func<T?, CancellationToken, Task> cancelableExecute, Predicate<T?> canExecute,
        AsyncRelayCommandOptions options)
    {
        ArgumentNullException.ThrowIfNull(cancelableExecute);
        ArgumentNullException.ThrowIfNull(canExecute);

        this.cancelableExecute = cancelableExecute;
        this.canExecute = canExecute;
        this.options = options;
    }

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <inheritdoc />
    public event EventHandler? CanExecuteChanged;

    /// <inheritdoc />
    public Task? ExecutionTask
    {
        get => executionTask;
        private set
        {
            if (ReferenceEquals(executionTask, value)) return;

            executionTask = value;

            PropertyChanged?.Invoke(this, AsyncRelayCommand.ExecutionTaskChangedEventArgs);
            PropertyChanged?.Invoke(this, AsyncRelayCommand.IsRunningChangedEventArgs);

            var isAlreadyCompletedOrNull = value?.IsCompleted ?? true;

            if (cancellationTokenSource is not null)
            {
                PropertyChanged?.Invoke(this, AsyncRelayCommand.CanBeCanceledChangedEventArgs);
                PropertyChanged?.Invoke(this, AsyncRelayCommand.IsCancellationRequestedChangedEventArgs);
            }

            if (isAlreadyCompletedOrNull) return;

            static async void MonitorTask(AsyncRelayCommand<T> @this, Task task)
            {
                await task.GetAwaitableWithoutEndValidation();

                if (ReferenceEquals(@this.executionTask, task))
                {
                    @this.PropertyChanged?.Invoke(@this, AsyncRelayCommand.ExecutionTaskChangedEventArgs);
                    @this.PropertyChanged?.Invoke(@this, AsyncRelayCommand.IsRunningChangedEventArgs);

                    if (@this.cancellationTokenSource is not null)
                        @this.PropertyChanged?.Invoke(@this, AsyncRelayCommand.CanBeCanceledChangedEventArgs);

                    if ((@this.options & AsyncRelayCommandOptions.AllowConcurrentExecutions) == 0)
                        @this.CanExecuteChanged?.Invoke(@this, EventArgs.Empty);
                }
            }

            MonitorTask(this, value!);
        }
    }

    /// <inheritdoc />
    public bool CanBeCanceled => IsRunning && cancellationTokenSource is { IsCancellationRequested: false };

    /// <inheritdoc />
    public bool IsCancellationRequested => cancellationTokenSource is { IsCancellationRequested: true };

    /// <inheritdoc />
    public bool IsRunning => ExecutionTask is { IsCompleted: false };

    /// <inheritdoc />
    public void NotifyCanExecuteChanged()
    {
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanExecute(T? parameter)
    {
        var canExecute = this.canExecute?.Invoke(parameter) != false;

        return canExecute && ((options & AsyncRelayCommandOptions.AllowConcurrentExecutions) != 0 ||
                              ExecutionTask is not { IsCompleted: false });
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool CanExecute(object? parameter)
    {
        // Special case, see RelayCommand<T>.CanExecute(object?) for more info
        if (parameter is null && default(T) is not null) return false;

        if (!RelayCommand<T>.TryGetCommandArgument(parameter, out var result))
            RelayCommand<T>.ThrowArgumentExceptionForInvalidCommandArgument(parameter);

        return CanExecute(result);
    }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Execute(T? parameter)
    {
        var executionTask = ExecuteAsync(parameter);

        if ((options & AsyncRelayCommandOptions.FlowExceptionsToTaskScheduler) == 0)
            AsyncRelayCommand.AwaitAndThrowIfFailed(executionTask);
    }

    /// <inheritdoc />
    public void Execute(object? parameter)
    {
        if (!RelayCommand<T>.TryGetCommandArgument(parameter, out var result))
            RelayCommand<T>.ThrowArgumentExceptionForInvalidCommandArgument(parameter);

        Execute(result);
    }

    /// <inheritdoc />
    public Task ExecuteAsync(T? parameter)
    {
        Task executionTask;

        if (execute is not null)
        {
            // Non cancelable command delegate
            executionTask = ExecutionTask = execute(parameter);
        }
        else
        {
            // Cancel the previous operation, if one is pending
            this.cancellationTokenSource?.Cancel();

            var cancellationTokenSource = this.cancellationTokenSource = new CancellationTokenSource();

            // Invoke the cancelable command delegate with a new linked token
            executionTask = ExecutionTask = cancelableExecute!(parameter, cancellationTokenSource.Token);
        }

        // If concurrent executions are disabled, notify the can execute change as well
        if ((options & AsyncRelayCommandOptions.AllowConcurrentExecutions) == 0)
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);

        return executionTask;
    }

    /// <inheritdoc />
    public Task ExecuteAsync(object? parameter)
    {
        if (!RelayCommand<T>.TryGetCommandArgument(parameter, out var result))
            RelayCommand<T>.ThrowArgumentExceptionForInvalidCommandArgument(parameter);

        return ExecuteAsync(result);
    }

    /// <inheritdoc />
    public void Cancel()
    {
        if (this.cancellationTokenSource is CancellationTokenSource
            {
                IsCancellationRequested: false
            } cancellationTokenSource)
        {
            cancellationTokenSource.Cancel();

            PropertyChanged?.Invoke(this, AsyncRelayCommand.CanBeCanceledChangedEventArgs);
            PropertyChanged?.Invoke(this, AsyncRelayCommand.IsCancellationRequestedChangedEventArgs);
        }
    }

    /// <inheritdoc />
    bool ICancellationAwareCommand.IsCancellationSupported => execute is null;
}