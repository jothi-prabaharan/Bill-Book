SELECT u.*, ur.* FROM mst."Users" u LEFT JOIN mst."UserOrganizationRoles" ur ON u."UserId" = ur."UserId" WHERE u."Email" = 'jothiprabaharan@gmail.com';
