```yaml
schema: gentle-ai.verify-result/v1
evidence_revision: sha256:e98535bf120b25a72201608e897d3285eb1ad1897bdbc2f2f15a5ac954379602
verdict: fail
blockers: 1
critical_findings: 1
requirements: 3/3
scenarios: 7/7
test_command: dotnet test Secop.Buscador.sln
test_exit_code: 124
test_output_hash: sha256:d9e3af5a4e7972a397b7418977b63ff9f5136c9deb50f876f32e4b2b6d1cd137
build_command: dotnet build Secop.Buscador.sln
build_exit_code: 0
build_output_hash: sha256:5021f7c717d35eab118a28d5e1cc22cf5a2d3a8b54c7e933a60ec07e1ed38a32
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

### Build & Tests Execution
| Command | Exit | Result | Runtime evidence hash |
|---|---:|---|---|
| `dotnet test tests/Secop.Domain.Tests/Secop.Domain.Tests.csproj` | 0 | 55 passed, 0 failed, 0 skipped | `sha256:62e1039db0da763adcdad7aa6873ed8c8974772caba4fafd37b4b24bbd4d5d7a` |
| `dotnet test tests/Secop.Application.Tests/Secop.Application.Tests.csproj` | 0 | 123 passed, 0 failed, 0 skipped | `sha256:f729e394e2c6c0f0f0f2cdac86b02de29dbd09b396a82b6f52ddeb11e340a341` |
| `dotnet test Secop.Buscador.sln` | 124 | Timed out after `Fatal error. Internal CLR error. (0x80131506)`; no test count available | `sha256:d9e3af5a4e7972a397b7418977b63ff9f5136c9deb50f876f32e4b2b6d1cd137` |
| `dotnet build Secop.Buscador.sln` | 0 | succeeded: 0 errors, 34 NU1903 package-vulnerability warnings | `sha256:5021f7c717d35eab118a28d5e1cc22cf5a2d3a8b54c7e933a60ec07e1ed38a32` |

Coverage analysis skipped — no coverage collector is configured. Build/type checking passed; no separate linter is configured.

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
| GREEN confirmed | ⚠️ | Focused suites pass, but the required solution test did not complete. |
| Triangulation adequate | ✅ | Boundaries, valid/invalid evidence, primary/additional paths, and three callers have distinct cases. |
| Safety Net for modified files | ✅ | Modified behavior files report approval-suite results; migration is structural. |

**TDD Compliance**: 5/6 checks passed.

### Test Layer Distribution
| Layer | Tests | Files | Tools |
|---|---:|---:|---|
| Unit | 35 focused parameterized cases | 6 | xUnit, FluentAssertions, Moq |
| Integration | 0 change-specific cases | 0 | solution command did not complete |
| E2E | 0 | 0 | not installed |
| **Total** | **35 focused cases** | **6** | |

### Changed File Coverage
Coverage analysis skipped — no coverage collector is configured. Coverage is informational and not a blocker.

### Assertion Quality
**Assertion quality**: ✅ All changed-test assertions inspected verify production behavior; no tautologies, ghost loops, isolated type checks, or smoke-only assertions found.

### Issues Found
**CRITICAL**: `dotnet test Secop.Buscador.sln` did not complete; the execution emitted an internal CLR error and was terminated at 120 seconds. This blocks a passing final verification.

**WARNING**: Build output contains 34 NU1903 dependency-vulnerability warnings outside this change.

**SUGGESTION**: Add a change-specific Infrastructure migration/DDL assertion for nullable `codigo_principal_categoria`.

### Verdict
FAIL
All 3 requirements and all 7 independently mapped scenarios have passing focused runtime coverage, but the required solution test did not complete, so final verification cannot pass.
