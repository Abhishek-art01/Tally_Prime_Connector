using TallyPrimeConnector.Contracts;
using TallyPrimeConnector.Core;
namespace Core.Tests;
public sealed class JobStateMachineTests { [Fact] public void Pending_can_start() => Assert.True(JobStateMachine.CanTransition(JobStatus.Pending, JobStatus.Running)); [Fact] public void Completed_cannot_restart() => Assert.False(JobStateMachine.CanTransition(JobStatus.Completed, JobStatus.Running)); }
