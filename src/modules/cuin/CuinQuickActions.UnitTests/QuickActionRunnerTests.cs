// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// [fork-feature] QuickActionRunner 执行策略测试：危险动作确认闸、异常隔离、幂等重复调用。
using System;
using System.Threading.Tasks;
using CuinQuickActions.Common.Actions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CuinQuickActions.UnitTests
{
    [TestClass]
    public class QuickActionRunnerTests
    {
        private static QuickActionDefinition NonDestructive => ActionCatalog.FindById("open_task_manager")!;

        private static QuickActionDefinition Destructive => ActionCatalog.FindById("restart_explorer")!;

        [TestMethod]
        public async Task DestructiveAction_WithoutConfirmation_IsRejectedAndNotExecuted()
        {
            var executor = new RecordingExecutor();
            var runner = new QuickActionRunner(executor);

            var result = await runner.ExecuteAsync(Destructive, userConfirmed: false);

            Assert.AreEqual(QuickActionExecutionStatus.RejectedNeedsConfirmation, result.Status);
            Assert.AreEqual(0, executor.Invocations.Count, "未经确认的危险动作绝不能触达执行器");
        }

        [TestMethod]
        public async Task DestructiveAction_WithConfirmation_ExecutesOnce()
        {
            var executor = new RecordingExecutor();
            var runner = new QuickActionRunner(executor);

            var result = await runner.ExecuteAsync(Destructive, userConfirmed: true);

            Assert.AreEqual(QuickActionExecutionStatus.Success, result.Status);
            Assert.AreEqual(1, executor.Invocations.Count);
            Assert.AreEqual("restart_explorer", executor.Invocations[0].Id);
        }

        [TestMethod]
        public async Task NonDestructiveAction_ExecutesWithoutConfirmation()
        {
            var executor = new RecordingExecutor();
            var runner = new QuickActionRunner(executor);

            var result = await runner.ExecuteAsync(NonDestructive, userConfirmed: false);

            Assert.AreEqual(QuickActionExecutionStatus.Success, result.Status);
            Assert.AreEqual(1, executor.Invocations.Count);
        }

        [TestMethod]
        public async Task ExecutorReturningFalse_YieldsFailed()
        {
            var executor = new RecordingExecutor { NextResult = false };
            var runner = new QuickActionRunner(executor);

            var result = await runner.ExecuteAsync(NonDestructive, userConfirmed: false);

            Assert.AreEqual(QuickActionExecutionStatus.Failed, result.Status);
        }

        [TestMethod]
        public async Task ExecutorThrowing_IsContainedAsFailed()
        {
            var executor = new RecordingExecutor { ThrowOnExecute = new InvalidOperationException("boom") };
            var runner = new QuickActionRunner(executor);

            var result = await runner.ExecuteAsync(NonDestructive, userConfirmed: false);

            Assert.AreEqual(QuickActionExecutionStatus.Failed, result.Status);
        }

        [TestMethod]
        public async Task UnknownAction_IsRejected()
        {
            var executor = new RecordingExecutor();
            var runner = new QuickActionRunner(executor);

            var ghost = new QuickActionDefinition
            {
                Id = "not_in_catalog",
                Kind = QuickActionKind.LaunchUri,
                Group = QuickActionGroup.System,
                Glyph = "\uE000",
                Uri = "ms-settings:",
            };

            var result = await runner.ExecuteAsync(ghost, userConfirmed: false);

            Assert.AreEqual(QuickActionExecutionStatus.UnknownAction, result.Status);
            Assert.AreEqual(0, executor.Invocations.Count);
        }

        [TestMethod]
        public async Task RepeatedInvocation_IsForwardedEveryTime()
        {
            var executor = new RecordingExecutor();
            var runner = new QuickActionRunner(executor);

            await runner.ExecuteAsync(NonDestructive, userConfirmed: false);
            await runner.ExecuteAsync(NonDestructive, userConfirmed: false);
            await runner.ExecuteAsync(NonDestructive, userConfirmed: false);

            Assert.AreEqual(3, executor.Invocations.Count, "重复调用必须每次转发（无意外去重/阻塞）");
        }

        [TestMethod]
        public async Task NullAction_ThrowsArgumentNullException()
        {
            var runner = new QuickActionRunner(new RecordingExecutor());
            await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => runner.ExecuteAsync(null!, userConfirmed: false));
        }

        [TestMethod]
        public void RequiresConfirmation_MatchesDestructiveFlag()
        {
            Assert.IsTrue(QuickActionRunner.RequiresConfirmation(Destructive));
            Assert.IsFalse(QuickActionRunner.RequiresConfirmation(NonDestructive));
        }
    }
}
