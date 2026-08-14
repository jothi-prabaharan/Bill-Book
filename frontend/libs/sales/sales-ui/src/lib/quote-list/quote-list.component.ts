import { Component, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { QuoteService, QuoteListItem } from '@bill-book/sales-core';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'bb-quote-list',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './quote-list.component.html',
  styleUrls: ['./quote-list.component.scss']
})
export class QuoteListComponent implements OnInit {
  private quoteService = inject(QuoteService);
  quotes: QuoteListItem[] = [];

  ngOnInit() {
    this.quoteService.list().subscribe(q => this.quotes = q);
  }
}
