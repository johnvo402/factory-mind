import { DecimalPipe } from '@angular/common';
import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { KnowledgeStore } from './knowledge.store';

@Component({
  selector: 'app-knowledge-workspace',
  imports: [DecimalPipe],
  templateUrl: './knowledge-workspace.component.html',
  styleUrl: './knowledge-workspace.component.scss',
})
export class KnowledgeWorkspaceComponent implements OnInit, OnDestroy {
  protected readonly store = inject(KnowledgeStore);
  protected readonly title = signal('');
  protected readonly selectedFile = signal<File | null>(null);
  protected readonly query = signal('');
  private pollId: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    void this.store.load();
    this.pollId = setInterval(() => {
      if (this.store.documents().some(document =>
        document.status === 'uploaded' || document.status === 'processing')) {
        void this.store.load(true);
      }
    }, 3_000);
  }

  ngOnDestroy(): void {
    if (this.pollId) {
      clearInterval(this.pollId);
    }
  }

  protected chooseFile(event: Event): void {
    this.selectedFile.set((event.target as HTMLInputElement).files?.[0] ?? null);
  }

  protected async upload(): Promise<void> {
    const file = this.selectedFile();
    if (file && await this.store.upload(file, this.title())) {
      this.selectedFile.set(null);
      this.title.set('');
    }
  }

  protected search(): void {
    if (this.query().trim()) {
      void this.store.search(this.query());
    }
  }

  protected formatSize(bytes: number): string {
    return bytes < 1024 * 1024
      ? `${Math.ceil(bytes / 1024)} KB`
      : `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
