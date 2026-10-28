"use strict";
document.querySelectorAll('form[method="post"]').forEach(form=>{
  form.addEventListener("submit",()=>{
    if(form.hasAttribute("novalidate")||form.checkValidity()){
      form.querySelectorAll('button[type="submit"]').forEach(button=>{
        button.disabled=true;button.setAttribute("aria-busy","true");
      });
    }
  });
});
