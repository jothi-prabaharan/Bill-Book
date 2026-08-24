import { ChangeDetectionStrategy } from '@angular/core';
import { Component } from '@angular/core';
import { ContactPersonRolesList } from '../contact-person-roles-list/contact-person-roles.list';

/**
 * Settings › Contact person roles.
 *
 * The same master the contact form opens as a popup, shown where somebody
 * setting a branch up will look for it — beside payment terms, unit types and
 * the rest. The popup exists so a role can be added without abandoning a
 * half-typed contact; this exists because that is not where anybody goes to
 * review a master.
 *
 * Both render <see cref="ContactPersonRolesList"/>, so there is one list and one
 * place to change its behaviour.
 */
changeDetection: ChangeDetectionStrategy.OnPush,
  selector: 'bb-contact-person-roles-page',
  standalone: true,
  imports: [ContactPersonRolesList],
  templateUrl: './contact-person-roles.page.html',
  styleUrl: './contact-person-roles.page.scss',
})
export class ContactPersonRolesPage {}


