using System.Collections.Immutable;
using System.Diagnostics;

namespace Xenial.RTool.Common;

public record MiddlewareBuilder<TDelegate, TBuilder>
    : MiddlewareBuilder<TDelegate, Unit, TBuilder>
    where TBuilder : MiddlewareBuilder<TDelegate, TBuilder>
    where TDelegate : Delegate
{
    public MiddlewareBuilder(TDelegate initialDelegate) : base(Unit.Value, initialDelegate)
    {

    }
}

public record MiddlewareBuilder<TDelegate, TContext, TBuilder>
    where TBuilder : MiddlewareBuilder<TDelegate, TContext, TBuilder>
    where TDelegate : System.Delegate
{
    private static readonly object locker = new();

    private ImmutableArray<Func<TDelegate, TDelegate>> middlewares
        = ImmutableArray.Create<Func<TDelegate, TDelegate>>();

    public TContext Context { get; init; }

    private TDelegate InitialDelegate { get; }

    public Func<TDelegate, TDelegate, TDelegate>? Delegate { get; init; }

    public MiddlewareBuilder(TContext context, TDelegate initialDelegate)
    {
        Context = context ?? throw new ArgumentNullException(nameof(context));
        InitialDelegate = initialDelegate ?? throw new ArgumentNullException(nameof(initialDelegate));
    }

    [DebuggerStepThrough]
    public TBuilder Use(Func<TDelegate, TDelegate> middleware)
    {
        lock (locker)
        {
            middlewares = middlewares.Add(middleware);
        }

        return (TBuilder)this;
    }

    [DebuggerStepThrough]
    public TBuilder UseMiddleware<TMiddleware>(Func<TMiddleware> createMiddleware)
    {
        var methodInfo = typeof(TMiddleware).GetMethod("InvokeAsync");
        if (methodInfo is null)
        {
            throw new InvalidOperationException($"{typeof(TMiddleware)} must have an method called 'InvokeAsync' that matches the '{typeof(TMiddleware).FullName}' signiture.");
        }

        return Use(next =>
        {
            var target = createMiddleware();
            var info = methodInfo.CreateDelegate<TDelegate>(target);

            if (Delegate is null)
            {
                _ = info.DynamicInvoke(Context);
                return next;
            }

            return Delegate(info, next);
        });
    }

    [DebuggerStepThrough]
    public TDelegate Build()
    {
        TDelegate app = InitialDelegate;

        foreach (var middleware in middlewares.Reverse())
        {
            app = middleware(app);
        }

        return app;
    }
}