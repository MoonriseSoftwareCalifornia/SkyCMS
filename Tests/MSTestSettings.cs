// Enable phased parallel execution for the optimized test rollout.
[assembly: Parallelize(Workers = 6, Scope = ExecutionScope.MethodLevel)]
