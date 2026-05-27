const inner = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2
});

// Spend is always non-negative. Clamp before formatting so IEEE-754 -0 (produced by
// Math.ceil(value * 100 - epsilon) when value <= 0) cannot leak as "-$0.00" through
// Intl.NumberFormat's sign-preserving handling of -0.
// The 1e-9 epsilon absorbs IEEE-754 drift from `value * 100` so exact-cent inputs like
// 0.10 don't round up to 0.11; the ceiling makes sub-cent positive spend ($0.00013) show
// as $0.01 instead of $0.00.
export const usdFormatter = {
  format: (value: number) => {
    if (!Number.isFinite(value) || value <= 0) return inner.format(0);
    return inner.format(Math.ceil(value * 100 - 1e-9) / 100);
  }
};
