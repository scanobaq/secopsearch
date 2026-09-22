```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:b57d90cd82d32f9488b29a0185bb55f35b4dddef59c9f06a30d64ed8f2ed14d5
verdict: pass
blockers: 0
critical_findings: 0
requirements: 3/3
scenarios: 7/7
test_command: dotnet test Secop.Buscador.sln
test_exit_code: 0
test_output_hash: sha256:f96195d646daaa4aa4c853c5876cd1c4c126fce2a84d3ed2cbdc5805dd1a5fb3
build_command: dotnet build Secop.Buscador.sln
build_exit_code: 0
build_output_hash: sha256:801c1046d2d6187a11b7707a465588f88477ddf39212d5dbd3b06c9e4eebd4e0
```

## Verification Report

**Change**: hybrid-unspsc-admission
**Mode**: Strict TDD

### Completeness
| Metric | Value |
|---|---:|
| Tasks total | 15 |
| Tasks complete | 15 |
| Tasks incomplete | 0 |

Unchecked implementation task lines: none.

### Build & Tests Execution
| Command | Exit | Result | Runtime evidence hash |
|---|---:|---|---|
| `dotnet test tests/Secop.Domain.Tests/Secop.Domain.Tests.csproj` | 0 | 55 passed, 0 failed, 0 skipped | `sha256:c38f344592349417a0152f3bb8b16a73f103d0ca24eb6eed6787f15584ab3f53` |
| `dotnet test tests/Secop.Application.Tests/Secop.Application.Tests.csproj` | 0 | 123 passed, 0 failed, 0 skipped | `sha256:3a080a199b25286a8e4aa5a7e9fc43fb3d8cbffd74fc192ba118112491f2847c` |
| `dotnet test Secop.Buscador.sln` | 0 | Domain 55, Application 123, Infrastructure 56; 234 passed, 0 failed | `sha256:f96195d646daaa4aa4c853c5876cd1c4c126fce2a84d3ed2cbdc5805dd1a5fb3` |
| `dotnet build Secop.Buscador.sln` (initial attempt) | 134 | `Fatal error. Internal CLR error. (0x80131506)` | `sha256:d9e3af5a4e7972a397b7418977b63ff9f5136c9deb50f876f32e4b2b6d1cd137` |
| `dotnet build Secop.Buscador.sln` (retry 1) | 0 | succeeded: 0 errors, 34 NU1903 package-vulnerability warnings | `sha256:801c1046d2d6187a11b7707a465588f88477ddf39212d5dbd3b06c9e4eebd4e0` |

Coverage analysis skipped — no coverage collector is configured. Build/type checking passed on retry; no separate linter is configured.

### Spec Compliance Matrix
| Requirement | Scenario | Passing covering runtime test(s) | Result |
|---|---|---|---|
| Hybrid Semantic and UNSPSC Admission | High-confidence similarity is admitted | `PoliticaEvaluacionTests.Admitir_AplicaUmbralesYEvidenciaDeClase` (`0.45`) in passed Domain suite | ✅ COMPLIANT |
| Hybrid Semantic and UNSPSC Admission | Borderline similarity requires any category match | `PoliticaEvaluacionTests.Admitir_AplicaUmbralesYEvidenciaDeClase` (additional match) in passed Domain suite | ✅ COMPLIANT |
| Hybrid Semantic and UNSPSC Admission | Lower-bound and below-floor boundaries are enforced | `PoliticaEvaluacionTests.Admitir_AplicaUmbralesYEvidenciaDeClase` (`0.40` unmatched; `0.399999`) in passed Domain suite | ✅ COMPLIANT |
| Canonical UNSPSC Evidence | Prefixed primary code is persisted and matches | `SecopProcesoDtoTests.ObtenerCodigosUnspsc_NormalizaPrefijosYDescartaValoresInvalidos`; `SincronizarProcesosHandlerTests.Handle_SimilitudBorderlineConClasePrincipalCoincidente_PersisteYPuntua` in passed Application suite | ✅ COMPLIANT |
| Canonical UNSPSC Evidence | Invalid evidence cannot lower the threshold | `CodigoUnspscTests.TryCreate_ValorNuloSentinelaOMalformado_NoCreaCodigo`; `PoliticaEvaluacionTests.Admitir_EvidenciaInvalidaNoReduceElUmbral`; `SincronizarProcesosHandlerTests.Handle_SimilitudBorderlineConEvidenciaInvalida_NoPersisteNiPuntuaOAlerta` in passed suites | ✅ COMPLIANT |
| Consistent Admission and Historical Preservation | Every scoring path has parity | `SincronizarProcesosHandlerTests.Handle_SimilitudBorderlineConClasePrincipalCoincidente_PersisteYPuntua`; `EvaluacionProcesosHandlersTests.CalcularPuntaje_YRecalcular_RechazanBorderlineSinEvidenciaYSinPersistir`; `EvaluacionProcesosHandlersTests.CalcularPuntaje_YRecalcular_AdmitenCoincidenciaAdicionalParaProcesoLegacy` in passed Application suite | ✅ COMPLIANT |
| Consistent Admission and Historical Preservation | Legacy and historical data remain intact | `ProcesoTests.Constructor_CodigoPrincipalOpcional_ConservaCompatibilidadPosicional`; `EvaluacionProcesosHandlersTests.CalcularPuntaje_YRecalcular_AdmitenCoincidenciaAdicionalParaProcesoLegacy`; `EvaluacionProcesosHandlersTests.CalcularPuntaje_YRecalcular_RechazanBorderlineSinEvidenciaYSinPersistir` in passed suites | ✅ COMPLIANT |

**Compliance summary**: 7/7 scenarios compliant.

### Correctness
| Requirement | Status | Notes |
|---|---|---|
| Hybrid admission | ✅ Implemented | `PoliticaEvaluacion.Admitir` rejects `<0.40`, directly admits `>=0.45`, and otherwise compares valid six-digit classes across provider, primary, and additional codes. |
| Canonical evidence and persistence | ✅ Implemented | `CodigoUnspsc` accepts exactly eight ASCII digits; DTO strips trimmed `V1.`/`V1`; nullable primary is mapped and migrated. |
| Flow parity and history | ✅ Implemented | Synchronization, ad-hoc scoring, and recalculation invoke the shared Domain policy; no removal path was introduced. |

### Design Coherence
| Decision | Followed? | Notes |
|---|---|---|
| Pure Domain policy | ✅ Yes | `PoliticaEvaluacion.Admitir` is shared by all three scoring callers. |
| Strict ASCII parser and source-only V1 normalization | ✅ Yes | ASCII range check is explicit; normalization remains in `SecopProcesoDto`. |
| Nullable backward-compatible persistence | ✅ Yes | Optional constructor parameter is appended; EF mapping, migration, designer, and snapshot carry nullable `codigo_principal_categoria`. |
| Broad retrieval and historical preservation | ✅ Yes | No retrieval change is present; recalculation skips newly inadmissible pairs without deletion or revocation. |

### TDD Compliance
| Check | Result | Details |
|---|---|---|
| TDD evidence reported | ✅ | `apply-progress.md` contains all 15 task rows. |
| All tasks have test or structural evidence | ✅ | 15/15 behavioral tasks name existing tests; generated migration and verification tasks link structural or aggregate evidence. |
| RED confirmed | ✅ | Relevant test files exist and predecessor RED evidence is recorded for behavior-changing tasks. |
| GREEN confirmed | ✅ | Focused suites and the required solution test pass now. |
| Triangulation adequate | ✅ | Boundaries, valid/invalid evidence, primary/additional paths, and three callers have distinct cases. |
| Safety Net for modified files | ✅ | Modified behavior files report approval-suite results; migration is structural. |

**TDD Compliance**: 6/6 checks passed.

### Test Layer Distribution
| Layer | Tests | Files | Tools |
|---|---:|---:|---|
| Unit | 35 focused parameterized cases | 6 | xUnit, FluentAssertions, Moq |
| Integration | 0 change-specific cases | 0 | solution also passed 56 existing Infrastructure tests |
| E2E | 0 | 0 | not installed |
| **Total** | **35 focused cases** | **6** | |

### Changed File Coverage
Coverage analysis skipped — no coverage collector is configured. Coverage is informational and not a blocker.

### Assertion Quality
**Assertion quality**: ✅ All changed-test assertions inspected verify production behavior; no tautologies, ghost loops, isolated type checks, or smoke-only assertions found.

### Issues Found
No CRITICAL findings or blockers.

**WARNING**: The first fresh build attempt exited 134 with intermittent CLR `0x80131506`; immediate retry passed. This remains environment-flakiness evidence but does not invalidate the independently passing solution test and successful build retry.

**WARNING**: Passing restore/build output contains 34 NU1903 dependency-vulnerability warnings outside this change.

**SUGGESTION**: Add a change-specific Infrastructure migration/DDL assertion for nullable `codigo_principal_categoria`.

### Structured Status and Action Context
Native status selected exactly `hybrid-unspsc-admission`, reports apply `all_done`, verify `ready`, and 15/15 tasks. Mode is `repo-local`; workspace and allowed root both resolve to `/home/esneider-cano/repositories/personal/secopsearch`. Representative implementation and test files are tracked inside that authoritative workspace. The unrelated `src/Secop.Api/appsettings.json` user modification was preserved unchanged.

### Review Workload / PR Boundary
The task forecast required a cohesive single PR with `size:exception`; apply-progress explicitly records approval for 885 implementation lines. Verification found no unchecked tasks or evidence of work outside that boundary. This remediation changes only the authorized verify report.

### Verdict
PASS
All 3 requirements and all 7 scenarios are compliant; 15/15 tasks are checked; strict-TDD evidence is complete; focused suites, the required 234-test solution run, and the build retry pass. Maintainer-reported deployment success is contextual only and was not substituted for observed command evidence.
