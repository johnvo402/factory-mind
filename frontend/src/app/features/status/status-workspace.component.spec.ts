import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, provideRouter } from '@angular/router';
import { StatusWorkspaceComponent } from './status-workspace.component';

describe('StatusWorkspaceComponent', () => {
  it('renders route-provided status details and recovery links', async () => {
    await TestBed.configureTestingModule({
      imports: [StatusWorkspaceComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            snapshot: {
              data: {
                code: '404',
                eyebrow: 'KHÔNG TÌM THẤY',
                title: 'Trang bạn tìm không tồn tại',
                description: 'Đường dẫn không hợp lệ.',
              },
            },
          },
        },
      ],
    }).compileComponents();

    const fixture = TestBed.createComponent(StatusWorkspaceComponent);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('404');
    expect(fixture.nativeElement.textContent).toContain('Trang bạn tìm không tồn tại');
    expect(fixture.nativeElement.querySelectorAll('a').length).toBe(2);
  });
});
