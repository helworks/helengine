namespace helengine.bepu.tests {
    /// <summary>
    /// Compares managed and native reduced-BEPU differential traces and reports the first mismatch with field-level detail.
    /// </summary>
    public static class BepuDifferentialTraceComparer {
        /// <summary>
        /// Finds the first mismatch between two structured trace record sets after sorting them into a stable comparison order.
        /// </summary>
        /// <param name="managedRecords">Managed golden-trace records.</param>
        /// <param name="nativeRecords">Native package-trace records.</param>
        /// <param name="floatTolerance">Maximum tolerated absolute floating-point difference.</param>
        /// <returns>Empty string when the traces match; otherwise one field-level mismatch description.</returns>
        public static string FindFirstMismatch(IReadOnlyList<BepuDifferentialTraceRecord3D> managedRecords, IReadOnlyList<BepuDifferentialTraceRecord3D> nativeRecords, float floatTolerance = 0.0005f) {
            if (managedRecords == null) {
                throw new ArgumentNullException(nameof(managedRecords));
            }
            if (nativeRecords == null) {
                throw new ArgumentNullException(nameof(nativeRecords));
            }
            if (floatTolerance < 0f) {
                throw new ArgumentOutOfRangeException(nameof(floatTolerance));
            }

            List<BepuDifferentialTraceRecord3D> orderedManagedRecords = managedRecords
                .OrderBy(GetSortKey)
                .ToList();
            List<BepuDifferentialTraceRecord3D> orderedNativeRecords = nativeRecords
                .OrderBy(GetSortKey)
                .ToList();

            if (orderedManagedRecords.Count != orderedNativeRecords.Count) {
                return $"Record count mismatch: managed={orderedManagedRecords.Count} native={orderedNativeRecords.Count}.";
            }

            for (int index = 0; index < orderedManagedRecords.Count; index++) {
                BepuDifferentialTraceRecord3D managedRecord = orderedManagedRecords[index];
                BepuDifferentialTraceRecord3D nativeRecord = orderedNativeRecords[index];
                string mismatch = CompareRecord(managedRecord, nativeRecord, floatTolerance);
                if (!string.IsNullOrEmpty(mismatch)) {
                    return $"Mismatch at record {index} ({BuildRecordKey(managedRecord)}): {mismatch}";
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// Compares two records field by field and returns one mismatch description when they differ.
        /// </summary>
        /// <param name="managedRecord">Managed golden-trace record.</param>
        /// <param name="nativeRecord">Native package-trace record.</param>
        /// <param name="floatTolerance">Maximum tolerated absolute floating-point difference.</param>
        /// <returns>Empty string when the records match; otherwise one mismatch description.</returns>
        static string CompareRecord(BepuDifferentialTraceRecord3D managedRecord, BepuDifferentialTraceRecord3D nativeRecord, float floatTolerance) {
            if (managedRecord.Frame != nativeRecord.Frame) {
                return BuildScalarMismatch("frame", managedRecord.Frame, nativeRecord.Frame);
            }
            if (managedRecord.Phase != nativeRecord.Phase) {
                return BuildScalarMismatch("phase", managedRecord.Phase, nativeRecord.Phase);
            }
            if (managedRecord.BodyHandle != nativeRecord.BodyHandle) {
                return BuildScalarMismatch("body_handle", managedRecord.BodyHandle, nativeRecord.BodyHandle);
            }
            if (managedRecord.BodyIndex != nativeRecord.BodyIndex) {
                return BuildScalarMismatch("body_index", managedRecord.BodyIndex, nativeRecord.BodyIndex);
            }
            if (managedRecord.BundleIndex != nativeRecord.BundleIndex) {
                return BuildScalarMismatch("bundle_index", managedRecord.BundleIndex, nativeRecord.BundleIndex);
            }
            if (managedRecord.ConstraintBatchIndex != nativeRecord.ConstraintBatchIndex) {
                return BuildScalarMismatch("constraint_batch", managedRecord.ConstraintBatchIndex, nativeRecord.ConstraintBatchIndex);
            }
            if (managedRecord.TypeBatchIndex != nativeRecord.TypeBatchIndex) {
                return BuildScalarMismatch("type_batch", managedRecord.TypeBatchIndex, nativeRecord.TypeBatchIndex);
            }
            if (managedRecord.BodySlotIndex != nativeRecord.BodySlotIndex) {
                return BuildScalarMismatch("body_slot", managedRecord.BodySlotIndex, nativeRecord.BodySlotIndex);
            }
            if (!string.Equals(managedRecord.EncodedReferences, nativeRecord.EncodedReferences, StringComparison.Ordinal)) {
                return BuildScalarMismatch("encoded_refs", managedRecord.EncodedReferences, nativeRecord.EncodedReferences);
            }
            if (!string.Equals(managedRecord.IntegrationMask, nativeRecord.IntegrationMask, StringComparison.Ordinal)) {
                return BuildScalarMismatch("integration_mask", managedRecord.IntegrationMask, nativeRecord.IntegrationMask);
            }

            string vectorMismatch = CompareFloat3("position", managedRecord.Position, nativeRecord.Position, floatTolerance);
            if (!string.IsNullOrEmpty(vectorMismatch)) {
                return vectorMismatch;
            }

            string quaternionMismatch = CompareFloat4("orientation", managedRecord.Orientation, nativeRecord.Orientation, floatTolerance);
            if (!string.IsNullOrEmpty(quaternionMismatch)) {
                return quaternionMismatch;
            }

            string linearVelocityMismatch = CompareFloat3("linear_velocity", managedRecord.LinearVelocity, nativeRecord.LinearVelocity, floatTolerance);
            if (!string.IsNullOrEmpty(linearVelocityMismatch)) {
                return linearVelocityMismatch;
            }

            return CompareFloat3("angular_velocity", managedRecord.AngularVelocity, nativeRecord.AngularVelocity, floatTolerance);
        }

        /// <summary>
        /// Builds one stable sort key that keeps equivalent records in deterministic order across runtimes.
        /// </summary>
        /// <param name="record">Record to convert into one sort key.</param>
        /// <returns>Deterministic sort key text.</returns>
        static string GetSortKey(BepuDifferentialTraceRecord3D record) {
            return $"{record.Frame:D6}|{(int)record.Phase:D2}|{record.BodyHandle:D6}|{record.BodyIndex:D6}|{record.BundleIndex:D6}|{record.ConstraintBatchIndex:D6}|{record.TypeBatchIndex:D6}|{record.BodySlotIndex:D6}";
        }

        /// <summary>
        /// Builds one compact record key for mismatch reporting.
        /// </summary>
        /// <param name="record">Record to summarize.</param>
        /// <returns>Compact record key text.</returns>
        static string BuildRecordKey(BepuDifferentialTraceRecord3D record) {
            return $"frame={record.Frame} phase={record.Phase} body_handle={record.BodyHandle}";
        }

        /// <summary>
        /// Compares two three-component vectors using one absolute tolerance.
        /// </summary>
        /// <param name="fieldName">High-level field name being compared.</param>
        /// <param name="managedValue">Managed vector value.</param>
        /// <param name="nativeValue">Native vector value.</param>
        /// <param name="floatTolerance">Maximum tolerated absolute floating-point difference.</param>
        /// <returns>Empty string when the vectors match; otherwise one component-level mismatch description.</returns>
        static string CompareFloat3(string fieldName, float3 managedValue, float3 nativeValue, float floatTolerance) {
            if (Math.Abs(managedValue.X - nativeValue.X) > floatTolerance) {
                return BuildScalarMismatch(fieldName + ".x", managedValue.X, nativeValue.X);
            }
            if (Math.Abs(managedValue.Y - nativeValue.Y) > floatTolerance) {
                return BuildScalarMismatch(fieldName + ".y", managedValue.Y, nativeValue.Y);
            }
            if (Math.Abs(managedValue.Z - nativeValue.Z) > floatTolerance) {
                return BuildScalarMismatch(fieldName + ".z", managedValue.Z, nativeValue.Z);
            }

            return string.Empty;
        }

        /// <summary>
        /// Compares two four-component vectors using one absolute tolerance.
        /// </summary>
        /// <param name="fieldName">High-level field name being compared.</param>
        /// <param name="managedValue">Managed vector value.</param>
        /// <param name="nativeValue">Native vector value.</param>
        /// <param name="floatTolerance">Maximum tolerated absolute floating-point difference.</param>
        /// <returns>Empty string when the vectors match; otherwise one component-level mismatch description.</returns>
        static string CompareFloat4(string fieldName, float4 managedValue, float4 nativeValue, float floatTolerance) {
            if (Math.Abs(managedValue.X - nativeValue.X) > floatTolerance) {
                return BuildScalarMismatch(fieldName + ".x", managedValue.X, nativeValue.X);
            }
            if (Math.Abs(managedValue.Y - nativeValue.Y) > floatTolerance) {
                return BuildScalarMismatch(fieldName + ".y", managedValue.Y, nativeValue.Y);
            }
            if (Math.Abs(managedValue.Z - nativeValue.Z) > floatTolerance) {
                return BuildScalarMismatch(fieldName + ".z", managedValue.Z, nativeValue.Z);
            }
            if (Math.Abs(managedValue.W - nativeValue.W) > floatTolerance) {
                return BuildScalarMismatch(fieldName + ".w", managedValue.W, nativeValue.W);
            }

            return string.Empty;
        }

        /// <summary>
        /// Builds one scalar mismatch message using consistent `managed=` and `native=` labels.
        /// </summary>
        /// <param name="fieldName">Field name that diverged.</param>
        /// <param name="managedValue">Managed field value.</param>
        /// <param name="nativeValue">Native field value.</param>
        /// <returns>One scalar mismatch description.</returns>
        static string BuildScalarMismatch(string fieldName, object managedValue, object nativeValue) {
            return $"field={fieldName} managed={managedValue} native={nativeValue}";
        }
    }
}
