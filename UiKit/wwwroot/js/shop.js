// Ủy quyền sự kiện hoạt động cả sau khi Blazor điều hướng.
document.addEventListener('click',function(event){
 const open=event.target.closest('[data-open-dialog]');
 if(open){event.preventDefault();document.querySelectorAll('dialog[open]').forEach(x=>x.close());const id=open.getAttribute('data-open-dialog');const dialog=document.getElementById(id)||document.querySelector(id.startsWith('#')?id:'#'+CSS.escape(id));if(dialog)dialog.showModal();}
 const close=event.target.closest('[data-close-dialog]');if(close){event.preventDefault();close.closest('dialog')?.close();}
});
function electroShopOpenAuth(){const query=new URLSearchParams(location.search);if(query.has('login')||query.has('authError')||query.has('auth')){const id=query.get('auth')==='register'?'register-dialog':'login-dialog';const dialog=document.getElementById(id);if(dialog&&!dialog.open)dialog.showModal();}}
document.addEventListener('DOMContentLoaded',electroShopOpenAuth);document.addEventListener('enhancedload',electroShopOpenAuth);
// Xác nhận mật khẩu ở trình duyệt; backend vẫn kiểm tra lại bằng Data Annotations.
function checkPasswordConfirmation(form){const password=form.querySelector('input[name="Password"],input[name$=".Password"]');const confirm=form.querySelector('input[name="ConfirmPassword"],input[name$=".ConfirmPassword"]');if(password&&confirm){confirm.setCustomValidity(password.value===confirm.value?'':'Mật khẩu xác nhận không khớp.');}}
document.addEventListener('input',event=>{if(event.target.form)checkPasswordConfirmation(event.target.form);});
document.addEventListener('submit',event=>{checkPasswordConfirmation(event.target);if(!event.target.checkValidity()){event.preventDefault();event.target.reportValidity();}});
