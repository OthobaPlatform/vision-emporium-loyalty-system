import { describe, it, expect } from 'vitest';
import { validateCycleDates } from './CycleConfigSection';
import { validateThreshold, validateAllThresholds } from './ThresholdsConfigSection';
import { validateGeneralConfig } from './GeneralConfigSection';

describe('CycleConfigSection validation', () => {
  it('returns error when start date is empty', () => {
    const errors = validateCycleDates('', '2025-05-31');
    expect(errors.startDate).toBe('Start date is required');
  });

  it('returns error when end date is empty', () => {
    const errors = validateCycleDates('2024-06-01', '');
    expect(errors.endDate).toBe('End date is required');
  });

  it('returns error when end date is before start date', () => {
    const errors = validateCycleDates('2025-06-01', '2025-05-01');
    expect(errors.endDate).toBe('End date must be after start date');
  });

  it('returns error when end date equals start date', () => {
    const errors = validateCycleDates('2025-06-01', '2025-06-01');
    expect(errors.endDate).toBe('End date must be after start date');
  });

  it('returns error when duration is less than 30 days', () => {
    const errors = validateCycleDates('2025-06-01', '2025-06-20');
    expect(errors.general).toContain('at least 30 days');
  });

  it('returns error when duration exceeds 730 days', () => {
    const errors = validateCycleDates('2024-01-01', '2026-02-01');
    expect(errors.general).toContain('must not exceed 730 days');
  });

  it('accepts valid cycle with 30 days', () => {
    const errors = validateCycleDates('2025-06-01', '2025-07-01');
    expect(Object.keys(errors)).toHaveLength(0);
  });

  it('accepts valid cycle with 365 days', () => {
    const errors = validateCycleDates('2024-06-01', '2025-05-31');
    expect(Object.keys(errors)).toHaveLength(0);
  });

  it('accepts valid cycle with exactly 730 days', () => {
    const errors = validateCycleDates('2024-01-01', '2025-12-31');
    expect(Object.keys(errors)).toHaveLength(0);
  });
});

describe('ThresholdsConfigSection validation', () => {
  const baseThreshold = {
    tier: 1,
    requiredPurchases: 3,
    giftType: 'Cash_Return' as const,
    giftDescription: 'Cash back reward',
    giftValue: 500,
    isEnabled: true,
  };

  it('returns error when requiredPurchases is less than 1', () => {
    const errors = validateThreshold({ ...baseThreshold, requiredPurchases: 0 }, 0, []);
    expect(errors['threshold-0-purchases']).toContain('between 1 and 100');
  });

  it('returns error when requiredPurchases exceeds 100', () => {
    const errors = validateThreshold({ ...baseThreshold, requiredPurchases: 101 }, 0, []);
    expect(errors['threshold-0-purchases']).toContain('between 1 and 100');
  });

  it('returns error for duplicate requiredPurchases', () => {
    const thresholds = [
      { ...baseThreshold, requiredPurchases: 3 },
      { ...baseThreshold, tier: 2, requiredPurchases: 3 },
    ];
    const errors = validateThreshold(thresholds[1], 1, thresholds);
    expect(errors['threshold-1-purchases']).toContain('Duplicate');
  });

  it('returns error when giftDescription is empty', () => {
    const errors = validateThreshold({ ...baseThreshold, giftDescription: '' }, 0, []);
    expect(errors['threshold-0-description']).toBe('Gift description is required');
  });

  it('returns error when giftDescription exceeds 200 characters', () => {
    const errors = validateThreshold(
      { ...baseThreshold, giftDescription: 'a'.repeat(201) },
      0,
      []
    );
    expect(errors['threshold-0-description']).toContain('200 characters');
  });

  it('returns error when giftValue is less than 0.01', () => {
    const errors = validateThreshold({ ...baseThreshold, giftValue: 0 }, 0, []);
    expect(errors['threshold-0-value']).toContain('between 0.01 and 999,999.99');
  });

  it('returns error when giftValue exceeds 999999.99', () => {
    const errors = validateThreshold({ ...baseThreshold, giftValue: 1000000 }, 0, []);
    expect(errors['threshold-0-value']).toContain('between 0.01 and 999,999.99');
  });

  it('accepts valid threshold', () => {
    const errors = validateThreshold(baseThreshold, 0, [baseThreshold]);
    expect(Object.keys(errors)).toHaveLength(0);
  });

  it('validateAllThresholds returns error when more than 10 thresholds', () => {
    const thresholds = Array.from({ length: 11 }, (_, i) => ({
      ...baseThreshold,
      tier: i + 1,
      requiredPurchases: i + 1,
    }));
    const errors = validateAllThresholds(thresholds);
    expect(errors.general).toContain('Maximum 10');
  });

  it('validateAllThresholds returns error when no thresholds', () => {
    const errors = validateAllThresholds([]);
    expect(errors.general).toContain('At least 1');
  });
});

describe('GeneralConfigSection validation', () => {
  const validConfig = {
    syncIntervalMinutes: 60,
    codeExpiryDays: 30,
    minPurchaseAmount: 100,
    excludedCategories: [],
  };

  it('returns error when syncIntervalMinutes is less than 15', () => {
    const errors = validateGeneralConfig({ ...validConfig, syncIntervalMinutes: 14 });
    expect(errors.syncIntervalMinutes).toContain('at least 15 minutes');
  });

  it('accepts syncIntervalMinutes of exactly 15', () => {
    const errors = validateGeneralConfig({ ...validConfig, syncIntervalMinutes: 15 });
    expect(errors.syncIntervalMinutes).toBeUndefined();
  });

  it('returns error when codeExpiryDays is less than 7', () => {
    const errors = validateGeneralConfig({ ...validConfig, codeExpiryDays: 6 });
    expect(errors.codeExpiryDays).toContain('between 7 and 90');
  });

  it('returns error when codeExpiryDays exceeds 90', () => {
    const errors = validateGeneralConfig({ ...validConfig, codeExpiryDays: 91 });
    expect(errors.codeExpiryDays).toContain('between 7 and 90');
  });

  it('accepts codeExpiryDays of 7', () => {
    const errors = validateGeneralConfig({ ...validConfig, codeExpiryDays: 7 });
    expect(errors.codeExpiryDays).toBeUndefined();
  });

  it('accepts codeExpiryDays of 90', () => {
    const errors = validateGeneralConfig({ ...validConfig, codeExpiryDays: 90 });
    expect(errors.codeExpiryDays).toBeUndefined();
  });

  it('returns error when minPurchaseAmount is less than 0.01', () => {
    const errors = validateGeneralConfig({ ...validConfig, minPurchaseAmount: 0 });
    expect(errors.minPurchaseAmount).toContain('between 0.01 and 999,999.99');
  });

  it('returns error when minPurchaseAmount exceeds 999999.99', () => {
    const errors = validateGeneralConfig({ ...validConfig, minPurchaseAmount: 1000000 });
    expect(errors.minPurchaseAmount).toContain('between 0.01 and 999,999.99');
  });

  it('accepts valid general config', () => {
    const errors = validateGeneralConfig(validConfig);
    expect(Object.keys(errors)).toHaveLength(0);
  });
});
