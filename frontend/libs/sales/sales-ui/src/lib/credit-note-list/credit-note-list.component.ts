import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { CreditNoteService, CreditNoteListItem } from '@bill-book/sales-core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'bb-credit-note-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './credit-note-list.component.html',
  styleUrl: './credit-note-list.component.scss'
})
export class CreditNoteListComponent implements OnInit {
  private creditNoteService = inject(CreditNoteService);
  creditNotes: CreditNoteListItem[] = [];

  ngOnInit() {
    this.creditNoteService.list().subscribe(list => this.creditNotes = list);
  }
}
