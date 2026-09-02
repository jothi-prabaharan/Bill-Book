import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import {
  IonHeader,
  IonToolbar,
  IonTitle,
  IonContent,
  IonButton,
  IonItem,
  IonLabel,
  IonIcon,
  ModalController
} from '@ionic/angular/standalone';
import { addIcons } from 'ionicons';
import { documentTextOutline } from 'ionicons/icons';

@Component({
  selector: 'bb-statement-upload-form',
  standalone: true,
  imports: [
    CommonModule,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonContent,
    IonButton,
    IonItem,
    IonLabel,
    IonIcon
  ],
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-title>Upload Statement (CSV/OFX)</ion-title>
        <ion-button slot="end" fill="clear" (click)="dismiss()">Close</ion-button>
      </ion-toolbar>
    </ion-header>

    <ion-content class="ion-padding">
      <div 
        class="drop-zone" 
        (dragover)="onDragOver($event)" 
        (dragleave)="onDragLeave($event)" 
        (drop)="onDrop($event)"
        [class.drag-over]="isDragOver()"
        tabindex="0"
        (keyup.enter)="fileInput.click()"
        (click)="fileInput.click()">
        
        <ion-icon name="document-text-outline" size="large"></ion-icon>
        <p *ngIf="!selectedFile()">Drag & drop a CSV or OFX file here, or click to select</p>
        <p *ngIf="selectedFile()">Selected: {{ selectedFile()?.name }}</p>

        <input 
          #fileInput 
          type="file" 
          accept=".csv,.ofx" 
          style="display: none" 
          (change)="onFileSelected($event)">
      </div>

      <div class="ion-margin-top ion-text-center">
        <ion-button 
          (click)="upload()" 
          [disabled]="!selectedFile() || uploading()">
          {{ uploading() ? 'Uploading...' : 'Upload' }}
        </ion-button>
      </div>
    </ion-content>
  `,
  styles: [`
    .drop-zone {
      border: 2px dashed var(--ion-color-medium);
      border-radius: 8px;
      padding: 40px 20px;
      text-align: center;
      cursor: pointer;
      background: var(--ion-color-light);
      transition: all 0.2s ease;
    }
    .drop-zone.drag-over {
      border-color: var(--ion-color-primary);
      background: var(--ion-color-primary-tint);
    }
  `]
})
export class StatementUploadFormComponent {
  private readonly http = inject(HttpClient);
  private readonly modalCtrl = inject(ModalController);

  readonly isDragOver = signal(false);
  readonly selectedFile = signal<File | null>(null);
  readonly uploading = signal(false);

  constructor() {
    addIcons({ documentTextOutline });
  }

  onDragOver(event: DragEvent) {
    event.preventDefault();
    this.isDragOver.set(true);
  }

  onDragLeave(event: DragEvent) {
    event.preventDefault();
    this.isDragOver.set(false);
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.isDragOver.set(false);
    
    if (event.dataTransfer?.files.length) {
      this.selectedFile.set(event.dataTransfer.files[0]);
    }
  }

  onFileSelected(event: Event) {
    const target = event.target as HTMLInputElement;
    if (target.files && target.files.length) {
      this.selectedFile.set(target.files[0]);
    }
  }

  upload() {
    const file = this.selectedFile();
    if (!file) return;

    this.uploading.set(true);
    const formData = new FormData();
    formData.append('file', file);
    formData.append('bankAccountId', '1'); // For demo purposes

    // Assuming we have BankStatementsController with an upload endpoint
    this.http.post<{ bankStatementId: number }>('/api/bankstatements/upload', formData).subscribe({
      next: (res) => {
        this.uploading.set(false);
        this.modalCtrl.dismiss({ bankStatementId: res.bankStatementId });
      },
      error: () => {
        this.uploading.set(false);
      }
    });
  }

  dismiss() {
    this.modalCtrl.dismiss();
  }
}
