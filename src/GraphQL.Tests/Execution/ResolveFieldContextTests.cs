using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraphQL.Execution;
using Shouldly;
using Xunit;

namespace GraphQL.Tests.Execution
{
    public class ResolveFieldContextTests
    {
        private readonly ResolveFieldContext _context;

        public ResolveFieldContextTests()
        {
            _context = new ResolveFieldContext
            {
                Arguments = new Dictionary<string, ArgumentValue>(),
                Errors = new ExecutionErrors(),
                Extensions = new Dictionary<string, object>(),
            };
        }

        [Fact]
        public void argument_converts_int_to_long()
        {
            int val = 1;
            _context.Arguments["a"] = new ArgumentValue(val, ArgumentSource.Literal);
            var result = _context.GetArgument<long>("a");
            result.ShouldBe(1);
        }

        [Fact]
        public void argument_converts_long_to_int()
        {
            long val = 1;
            _context.Arguments["a"] = new ArgumentValue(val, ArgumentSource.Literal);
            var result = _context.GetArgument<int>("a");
            result.ShouldBe(1);
        }

        [Fact]
        public void long_to_int_should_throw_for_out_of_range()
        {
            long val = 89429901947254093;
            _context.Arguments["a"] = new ArgumentValue(val, ArgumentSource.Literal);
            Should.Throw<OverflowException>(() => _context.GetArgument<int>("a"));
        }

        [Fact]
        public void argument_returns_boxed_string_uncast()
        {
            var val = "one";
            _context.Arguments["a"] = new ArgumentValue(val, ArgumentSource.Literal);
            var result = _context.GetArgument<object>("a");
            result.ShouldBe("one");
        }

        [Fact]
        public void argument_returns_long()
        {
            long val = 1000000000000001;
            _context.Arguments["a"] = new ArgumentValue(val, ArgumentSource.Literal);
            var result = _context.GetArgument<long>("a");
            result.ShouldBe(1000000000000001);
        }

        [Fact]
        public void argument_returns_enum()
        {
            var val = SomeEnum.Two;
            _context.Arguments["a"] = new ArgumentValue(val, ArgumentSource.Literal);
            var result = _context.GetArgument<SomeEnum>("a");
            result.ShouldBe(SomeEnum.Two);
        }

        [Fact]
        public void argument_returns_enum_from_string()
        {
            var val = "two";
            _context.Arguments["a"] = new ArgumentValue(val, ArgumentSource.Literal);
            var result = _context.GetArgument<SomeEnum>("a");
            result.ShouldBe(SomeEnum.Two);
        }

        [Fact]
        public void argument_returns_enum_from_number()
        {
            var val = 1;
            _context.Arguments["a"] = new ArgumentValue(val, ArgumentSource.Literal);
            var result = _context.GetArgument<SomeEnum>("a");
            result.ShouldBe(SomeEnum.Two);
        }

        [Fact]
        public void argument_returns_default_when_missing()
        {
            _context.GetArgument<string>("wat").ShouldBeNull();
        }

        [Fact]
        public void argument_returns_provided_default_when_missing()
        {
            _context.GetArgument("wat", "foo").ShouldBe("foo");
        }

        [Fact]
        public void argument_returns_list_from_array()
        {
            _context.Arguments = new Dictionary<string, ArgumentValue>
            {
                { "a", new ArgumentValue(new string[] { "one", "two"}, ArgumentSource.Literal) }
            };
            var result = _context.GetArgument<List<string>>("a");
            result.ShouldNotBeNull();
            result.Count.ShouldBe(2);
            result[0].ShouldBe("one");
            result[1].ShouldBe("two");
        }

        [Fact]
        public void resolveFieldContextAdapter_throws_error_when_null()
        {
            Should.Throw<ArgumentNullException>(() =>
            {
                _ = new ResolveFieldContextAdapter<object>(null);
            });
        }

        [Fact]
        public void resolveFieldContextAdapter_throws_error_if_invalid_type()
        {
            var context = new ResolveFieldContext { Source = "test" };
            Should.Throw<ArgumentException>(() =>
            {
                _ = new ResolveFieldContextAdapter<int>(context);
            });
        }

        [Fact]
        public void resolveFieldContextAdapter_accepts_null_sources_ref()
        {
            var context = new ResolveFieldContext();
            var adapter = new ResolveFieldContextAdapter<string>(context);
            adapter.Source.ShouldBe(null);
        }

        [Fact]
        public void resolveFieldContextAdapter_accepts_null_sources_nullable()
        {
            var context = new ResolveFieldContext();
            var adapter = new ResolveFieldContextAdapter<int?>(context);
            adapter.Source.ShouldBe(null);
        }

        [Fact]
        public void resolveFieldContextAdapter_throws_error_for_null_values()
        {
            var context = new ResolveFieldContext();
            Should.Throw<ArgumentException>(() =>
            {
                _ = new ResolveFieldContextAdapter<int>(context);
            });
        }

        [Fact]
        public void GetSetExtension_Should_Throw_On_Null()
        {
            IResolveFieldContext context = null;
            Should.Throw<ArgumentNullException>(() => context.GetExtension("e"));
            Should.Throw<ArgumentNullException>(() => context.SetExtension("e", 1));

            context = new ResolveFieldContext();
            context.GetExtension("a").ShouldBe(null);
            context.GetExtension("a.b.c.d").ShouldBe(null);
            Should.Throw<ArgumentException>(() => context.SetExtension("e", 1));
        }

        [Fact]
        public void GetSetExtension_Should_Get_And_Set_Values()
        {
            _context.GetExtension("a").ShouldBe(null);
            _context.GetExtension("a.b.c.d").ShouldBe(null);

            _context.SetExtension("a", 5);
            _context.GetExtension("a").ShouldBe(5);

            _context.SetExtension("a.b.c.d", "value");
            _context.GetExtension("a.b.c.d").ShouldBe("value");
            var d = _context.GetExtension("a.b").ShouldBeOfType<Dictionary<string, object>>();
            d.Count.ShouldBe(1);

            _context.SetExtension("a.b.c", "override");
            _context.GetExtension("a.b.c.d").ShouldBe(null);
        }

        [Fact]
        public async Task ExecutionError_Should_Be_Thread_Safe()
        {
            var e = new CountdownEvent(2);

            var t1 = Task.Run(() =>
            {
                e.Signal();
                e.Wait();
                for (int i = 0; i < 5; ++i)
                    _context.Errors.Add(new ExecutionError("test"));
            });
            var t2 = Task.Run(() =>
            {
                e.Signal();
                e.Wait();
                for (int i = 0; i < 5; ++i)
                    _context.Errors.Add(new ExecutionError("test"));
            });

            await Task.WhenAll(t1, t2);

            _context.Errors.Count.ShouldBe(10);
        }

        private enum SomeEnum
        {
            One,
            Two
        }
    }
}
