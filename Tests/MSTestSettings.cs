// Enable phased parallel execution for the optimized test rollout.
[assembly: Parallelize(Workers = 4, Scope = ExecutionScope.MethodLevel)]
