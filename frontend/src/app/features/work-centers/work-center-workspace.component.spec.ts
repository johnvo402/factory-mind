import { validateWorkCenterCalendar } from './work-center-workspace.component';

describe('Work Center calendar validation', () => {
  it('accepts multiple non-overlapping shifts and a unique day off', () => {
    expect(validateWorkCenterCalendar(2, [
      { dayOfWeek: 1, startTime: '08:00:00', endTime: '12:00:00' },
      { dayOfWeek: 1, startTime: '13:00:00', endTime: '17:00:00' },
    ], [{ date: '2026-09-15' }])).toBe('');
  });

  it('rejects capacity bounds, invalid times, overlap, and duplicate day off', () => {
    expect(validateWorkCenterCalendar(0, [], [])).toContain('1 đến 100');
    expect(validateWorkCenterCalendar(1, [
      { dayOfWeek: 1, startTime: '17:00:00', endTime: '08:00:00' },
    ], [])).toContain('bắt đầu');
    expect(validateWorkCenterCalendar(1, [
      { dayOfWeek: 1, startTime: '08:00:00', endTime: '12:00:00' },
      { dayOfWeek: 1, startTime: '11:00:00', endTime: '17:00:00' },
    ], [])).toContain('chồng lấn');
    expect(validateWorkCenterCalendar(1, [], [
      { date: '2026-09-15' }, { date: '2026-09-15' },
    ])).toContain('không được trùng');
  });
});
