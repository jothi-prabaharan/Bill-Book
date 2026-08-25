import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import {
  IonButton,
  IonButtons,
  IonContent,
  IonHeader,
  IonInput,
  IonItem,
  IonLabel,
  IonTitle,
  IonToolbar,
  ModalController
} from '@ionic/angular/standalone';

@Component({
  selector: 'bb-fixed-asset-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    IonHeader,
    IonToolbar,
    IonTitle,
    IonContent,
    IonItem,
    IonLabel,
    IonInput,
    IonButton,
    IonButtons
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <ion-header>
      <ion-toolbar>
        <ion-title>Capitalize Asset</ion-title>
        <ion-buttons slot="end">
          <ion-button (click)="close()">Close</ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content class="ion-padding">
      <form [formGroup]="form" (ngSubmit)="save()">
        <ion-item>
          <ion-label position="stacked">Category ID</ion-label>
          <ion-input type="number" formControlName="fixedAssetCategoryId"></ion-input>
        </ion-item>
        
        <ion-item>
          <ion-label position="stacked">Asset Code</ion-label>
          <ion-input type="text" formControlName="assetCode"></ion-input>
        </ion-item>

        <ion-item>
          <ion-label position="stacked">Asset Name</ion-label>
          <ion-input type="text" formControlName="assetName"></ion-input>
        </ion-item>

        <ion-item>
          <ion-label position="stacked">Purchase Bill ID</ion-label>
          <ion-input type="number" formControlName="purchaseBillId"></ion-input>
        </ion-item>

        <ion-item>
          <ion-label position="stacked">Purchase Price</ion-label>
          <ion-input type="number" formControlName="purchasePrice"></ion-input>
        </ion-item>

        <ion-item>
          <ion-label position="stacked">Purchase Date</ion-label>
          <ion-input type="date" formControlName="purchaseDate"></ion-input>
        </ion-item>

        <div class="ion-margin-top">
          <ion-button expand="block" type="submit" [disabled]="!form.valid || saving()">
            {{ saving() ? 'Saving...' : 'Capitalize' }}
          </ion-button>
        </div>
      </form>
    </ion-content>
  `
})
export class FixedAssetFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly http = inject(HttpClient);
  private readonly modalCtrl = inject(ModalController);

  readonly saving = signal(false);

  readonly form = this.fb.nonNullable.group({
    fixedAssetCategoryId: [0, [Validators.required, Validators.min(1)]],
    assetCode: ['', Validators.required],
    assetName: ['', Validators.required],
    purchaseBillId: [0, Validators.required],
    purchasePrice: [0, [Validators.required, Validators.min(0.01)]],
    purchaseDate: ['', Validators.required]
  });

  close() {
    this.modalCtrl.dismiss();
  }

  save() {
    if (this.form.invalid) return;

    this.saving.set(true);
    this.http.post('/api/accounting/fixed-assets/capitalize', this.form.getRawValue()).subscribe({
      next: (result) => {
        this.saving.set(false);
        this.modalCtrl.dismiss(result);
      },
      error: () => {
        this.saving.set(false);
      }
    });
  }
}
