// Enable limited parallel execution during phased rollout.
[assembly: Parallelize(Workers = 2, Scope = ExecutionScope.MethodLevel)]
