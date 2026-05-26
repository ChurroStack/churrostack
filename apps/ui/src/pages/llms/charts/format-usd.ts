const inner = new Intl.NumberFormat('en-US', {
  style: 'currency',
  currency: 'USD',
  minimumFractionDigits: 2,
  maximumFractionDigits: 2
});

// Ceiling to the next cent so sub-cent spend (e.g. $0.00013) shows as $0.01 instead of $0.00.
// The 1e-9 epsilon absorbs IEEE-754 drift from `value * 100` so exact-cent inputs like 0.10 don't round up to 0.11.
export const usdFormatter = {
  format: (value: number) => inner.format(Math.ceil(value * 100 - 1e-9) / 100)
};
